// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务分支。对应 UE 的 UQuestBranch：一组任务 + 一个目标状态；任务全完成即取该分支、跳到目标状态。
// 与 UE 一致，UQuestBranch 也继承 UQuestNode(UNarrativeNodeBase)：激活时发 Start 事件、停用时发 End 事件。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 任务状态机里的一条分支。对应 UE <c>UQuestBranch</c>（继承 <c>UQuestNode</c>/<c>UNarrativeNodeBase</c>）。
    /// 激活时对所有任务 BeginTask 并触发继承而来的 <see cref="NarrativeNodeBase.Events"/>（Start 阶段）；
    /// 某任务达标时检查是否全部完成，若是则让 Quest 取此分支；停用时结束任务并触发 End 阶段事件。
    /// 典型用法：某分支“到手即给线索/开对话”写成分支的 Start 事件，“被取用时结算”写成 End 事件。
    /// 注：继承而来的 <see cref="NarrativeNodeBase.Conditions"/> 目前不参与分支取用判定（与 <see cref="QuestState"/> 一致，条件门控休眠）。
    /// </summary>
    [Serializable]
    public sealed class QuestBranch : NarrativeNodeBase
    {
        [Tooltip("是否在 UI 里隐藏（用于隐藏逻辑分支）。对应 UE bHidden。")]
        [SerializeField] private bool hidden;

        [Tooltip("取该分支后前往的目标状态 ID。对应 UE DestinationState。")]
        [SerializeField] private string destinationStateId;

        [Tooltip("需要完成的任务（全部完成才取此分支；顺序无关）。对应 UE QuestTasks。")]
        [SerializeReference] private List<QuestTask> tasks = new List<QuestTask>();

        private Quest _quest;
        private NarrativeContext _context;

        // 是否处于激活中。Deactivate 幂等的守卫：取分支时本分支会被 TakeBranch 与随后的旧状态 Deactivate 各调一次，
        // 靠此标记避免任务重复 End、事件重复触发（比 UE 更稳，UE 那条路径存在重复 Deactivate 的隐患）。
        private bool _active;

        public bool Hidden => hidden;

        public string DestinationStateId
        {
            get => destinationStateId ?? string.Empty;
            set => destinationStateId = value;
        }

        public IReadOnlyList<QuestTask> Tasks => tasks;

        public void AddTask(QuestTask task)
        {
            if (task != null)
            {
                (tasks ??= new List<QuestTask>()).Add(task);
            }
        }

        /// <summary>从模板克隆出干净的运行时副本（保留 id/hidden/目标状态/条件/事件配置，逐个克隆任务，清空运行时绑定）。</summary>
        internal QuestBranch CloneForRuntime()
        {
            // MemberwiseClone 复制 id/hidden/destinationStateId 及继承的 conditions/events 引用（只读配置，随克隆共享）。
            var clone = (QuestBranch)MemberwiseClone();
            clone._quest = null;
            clone._context = null;
            clone._active = false;
            clone.tasks = new List<QuestTask>();
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    if (task != null)
                    {
                        clone.tasks.Add(task.CloneForRuntime());
                    }
                }
            }

            return clone;
        }

        internal void Activate(Quest quest, NarrativeContext context)
        {
            _quest = quest;
            _context = context;
            _active = true;

            // 先 BeginTask，再触发本分支的 Start 事件（对齐 UE UQuestBranch::Activate → UQuestNode::Activate 顺序）。
            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    task?.Begin(this, context);
                }
            }

            // 读档重放时按 RefireOnLoad 过滤一次性事件（与状态事件一致）。
            ProcessEvents(context, EEventRuntime.Start, quest != null && quest.IsLoading);
        }

        internal void Deactivate()
        {
            // 幂等：已停用则直接返回，避免任务重复 End / 事件重复触发。
            if (!_active)
            {
                return;
            }

            _active = false;

            if (tasks != null)
            {
                foreach (var task in tasks)
                {
                    task?.End();
                }
            }

            // End 事件仅在任务仍进行中时触发（对齐 UE UQuestNode::Deactivate 的 QC_Started 守卫）：
            // 任务已结束后再清理分支不应再跑“离开”副作用。
            // 注：不清空 _quest——OnTaskReachedCompletion 会在 TakeBranch(→本 Deactivate) 之后仍用 _quest 广播 TaskCompleted（对齐 UE OwningQuest 不随 Deactivate 失效）。
            if (_quest == null || _quest.IsInProgress)
            {
                ProcessEvents(_context, EEventRuntime.End, _quest != null && _quest.IsLoading);
            }
        }

        /// <summary>所有任务是否都完成（可选任务恒视为完成）。对应 UE AreTasksComplete。</summary>
        public bool AreTasksComplete()
        {
            if (tasks == null)
            {
                return true;
            }

            foreach (var task in tasks)
            {
                if (task != null && !task.IsComplete)
                {
                    return false;
                }
            }

            return true;
        }

        // 任务进度变化/达标时的上行回调。
        internal void OnTaskProgressChanged(QuestTask task, int oldProgress, int newProgress)
        {
            _quest?.NotifyTaskProgress(this, task, oldProgress, newProgress);
        }

        internal void OnTaskReachedCompletion(QuestTask task)
        {
            // 对齐 UE UQuestBranch::OnQuestTaskComplete：先（若全完成）取分支，再广播任务完成。
            if (AreTasksComplete())
            {
                _quest?.TakeBranch(this);
            }

            _quest?.NotifyTaskCompleted(this, task);
        }
    }
}
