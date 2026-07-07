// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务项基类。对应 UE 的 UNarrativeTask（抽象、可蓝图扩展；分支的 QuestTasks 就是它的实例数组）。
// 有逻辑/进度，区别于纯数据的 DataTaskDefinition。子类覆盖 OnBeginTask/OnEndTask/Tick 写具体判定。
// 简化：UE 的导航标记(FTaskNavigationMarker)不移植（导航系统超范围）。

using System;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一个可完成的任务项。对应 UE <c>UNarrativeTask</c>。
    /// 进度到 <see cref="RequiredQuantity"/> 即完成；<see cref="Optional"/> 的任务在门控里恒视为完成。
    /// </summary>
    [Serializable]
    public abstract class QuestTask
    {
        [Tooltip("需要完成的次数。对应 UE RequiredQuantity。")]
        [SerializeField, Min(1)] private int requiredQuantity = 1;

        [Tooltip("是否可选：可选任务在分支完成判定里恒视为完成。对应 UE bOptional。")]
        [SerializeField] private bool optional;

        [Tooltip("是否在 UI 里隐藏。对应 UE bHidden。")]
        [SerializeField] private bool hidden;

        [Tooltip("轮询间隔秒数，0=不轮询。尽量用事件驱动而非轮询。对应 UE TickInterval。")]
        [SerializeField, Min(0f)] private float tickInterval;

        [Tooltip("覆盖自动生成的描述。对应 UE DescriptionOverride。")]
        [SerializeField] private string descriptionOverride;

        private int _currentProgress;
        private bool _isActive;
        private QuestBranch _branch;
        private NarrativeContext _context;
        private float _tickAccumulator;

        public int RequiredQuantity => requiredQuantity;
        public int CurrentProgress => _currentProgress;
        public bool Optional => optional;
        public bool Hidden => hidden;
        public float TickInterval => tickInterval;
        public bool IsActive => _isActive;

        /// <summary>当前上下文（供子类查询宿主等）。</summary>
        protected NarrativeContext Context => _context;

        /// <summary>供子类构造函数设置“需要完成次数”（代码构建/测试用；下限 1）。</summary>
        protected void SetRequiredQuantityForConstruction(int quantity)
        {
            requiredQuantity = Mathf.Max(1, quantity);
        }

        /// <summary>供子类构造函数设置“是否可选”（代码构建/测试用）。</summary>
        protected void SetOptionalForConstruction(bool isOptional)
        {
            optional = isOptional;
        }

        /// <summary>供子类构造函数设置轮询间隔秒数（代码构建/测试用；下限 0）。</summary>
        protected void SetTickIntervalForConstruction(float seconds)
        {
            tickInterval = Mathf.Max(0f, seconds);
        }

        /// <summary>是否已完成。对应 UE <c>IsComplete</c>：进度达标 或 可选。</summary>
        public bool IsComplete => _currentProgress >= requiredQuantity || optional;

        // ---------------- 生命周期（由 QuestBranch 调用）----------------

        internal void Begin(QuestBranch branch, NarrativeContext context)
        {
            _branch = branch;
            _context = context;
            _isActive = true;
            _tickAccumulator = 0f;
            OnBeginTask();
        }

        /// <summary>
        /// 按流逝时间驱动轮询：累积到 <see cref="TickInterval"/> 就调一次 <see cref="Tick"/>（可能一帧补多次）。
        /// 仅对激活中、且 <see cref="TickInterval"/> &gt; 0 的任务有效。由 <see cref="NarrativeComponent.TickActiveTasks"/> 调用。
        /// 对应 UE 用 TimerManager 按 TickInterval 重复调 TickTask——这里换成宿主每帧喂 deltaTime。
        /// 注：不移植 UE“BeginTask 后立即 tick 一次”的行为；初始检查请写在 <see cref="OnBeginTask"/> 里。
        /// </summary>
        internal void DriveTick(float deltaSeconds)
        {
            if (!_isActive || tickInterval <= 0f || deltaSeconds <= 0f)
            {
                return;
            }

            _tickAccumulator += deltaSeconds;
            // 防呆：间隔极小 + dt 极大时限制单帧补 tick 次数，避免卡死。
            int guard = 0;
            while (_tickAccumulator >= tickInterval && _isActive && guard++ < 1000)
            {
                _tickAccumulator -= tickInterval;
                Tick();
            }
        }

        internal void End()
        {
            _isActive = false;
            OnEndTask();
        }

        /// <summary>任务激活：绑定委托/初始化。对应 UE BeginTask。</summary>
        protected virtual void OnBeginTask() { }

        /// <summary>任务停用：解绑委托/清理。对应 UE EndTask。</summary>
        protected virtual void OnEndTask() { }

        /// <summary>轮询回调（仅当宿主按 <see cref="TickInterval"/> 驱动时）。尽量用事件替代。对应 UE TickTask。</summary>
        public virtual void Tick() { }

        // ---------------- 进度（子类驱动）----------------

        /// <summary>设置进度（clamp 到 [0, Required]）；变化则发进度事件，达标则通知分支。对应 UE SetProgressInternal。</summary>
        protected void SetProgress(int newProgress)
        {
            if (!_isActive || newProgress < 0)
            {
                return;
            }

            newProgress = Mathf.Clamp(newProgress, 0, requiredQuantity);
            if (newProgress == _currentProgress)
            {
                return;
            }

            var oldProgress = _currentProgress;
            _currentProgress = newProgress;

            _branch?.OnTaskProgressChanged(this, oldProgress, _currentProgress);

            // 用 >= Required 而非 IsComplete，避免可选任务的短路。对应 UE 的注释。
            if (_currentProgress >= requiredQuantity)
            {
                _branch?.OnTaskReachedCompletion(this);
            }
        }

        /// <summary>增加进度（可为负）。对应 UE AddProgress。</summary>
        protected void AddProgress(int delta = 1) => SetProgress(_currentProgress + delta);

        /// <summary>直接把进度拉满，完成任务。对应 UE CompleteTask。</summary>
        public void Complete() => SetProgress(requiredQuantity);

        /// <summary>读档用：直接写进度、不触发事件、不通知分支。</summary>
        internal void RestoreProgress(int progress)
        {
            _currentProgress = Mathf.Clamp(progress, 0, requiredQuantity);
        }

        /// <summary>
        /// 从模板克隆出一个干净的运行时副本：拷贝所有序列化配置（含子类字段），
        /// 但把运行时状态（进度/激活/绑定）清零。对应 UE 的“每次 BeginQuest 从 QuestClass 新建实例”。
        /// 用 <see cref="object.MemberwiseClone"/> 天然覆盖子类的私有序列化字段，无需子类各自实现。
        /// </summary>
        internal QuestTask CloneForRuntime()
        {
            var clone = (QuestTask)MemberwiseClone();
            clone._currentProgress = 0;
            clone._isActive = false;
            clone._branch = null;
            clone._context = null;
            return clone;
        }

        // ---------------- 描述 ----------------

        /// <summary>任务描述：优先覆盖文本，否则子类自动生成。对应 UE GetTaskDescription。</summary>
        public string GetDescription()
        {
            return string.IsNullOrEmpty(descriptionOverride) ? DefaultDescription() : descriptionOverride;
        }

        /// <summary>子类自动生成的默认描述。对应 UE GetTaskDescription 的自动生成。</summary>
        protected virtual string DefaultDescription() => GetType().Name;

        /// <summary>进度文本，如 "6/10"（Required 为 1 时为空）。对应 UE GetTaskProgressText。</summary>
        public virtual string GetProgressText()
        {
            return requiredQuantity > 1 ? $"{_currentProgress}/{requiredQuantity}" : string.Empty;
        }
    }
}
