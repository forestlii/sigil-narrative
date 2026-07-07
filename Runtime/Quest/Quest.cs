// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务运行时状态机。对应 UE 的 UQuest：持有状态集，从起始状态推进，任务完成即取分支到目标状态，
// 到达 Success/Failure 状态则结束。事件对应 UE TalesComponent 的 OnQuest* 委托群。

using System;
using System.Collections.Generic;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一个任务的运行时状态机。对应 UE <c>UQuest</c>。
    /// 用代码或（未来的）QuestAsset 构造出状态集，<see cref="Begin"/> 后由任务进度驱动状态流转。
    /// </summary>
    public sealed class Quest
    {
        private readonly Dictionary<string, QuestState> _statesById = new Dictionary<string, QuestState>();
        private readonly List<QuestState> _reached = new List<QuestState>();
        private readonly string _startStateId;
        private readonly NarrativeContext _context;

        public Quest(string startStateId, IEnumerable<QuestState> states, NarrativeContext context)
        {
            _startStateId = startStateId;
            _context = context;

            if (states != null)
            {
                foreach (var state in states)
                {
                    if (state != null && !string.IsNullOrEmpty(state.Id) && !_statesById.ContainsKey(state.Id))
                    {
                        _statesById[state.Id] = state;
                    }
                }
            }
        }

        /// <summary>本实例的来源资产（宿主用它作 quest 身份键，对应 UE 的 QuestClass）。由 <see cref="QuestAsset"/> 实例化时写入。</summary>
        public QuestAsset SourceAsset { get; internal set; }

        /// <summary>完成状态。对应 UE QuestCompletion。</summary>
        public EQuestCompletion Completion { get; private set; } = EQuestCompletion.NotStarted;

        /// <summary>当前所处状态。</summary>
        public QuestState CurrentState { get; private set; }

        /// <summary>到达过的状态（按顺序，含当前）。对应 UE ReachedStates。</summary>
        public IReadOnlyList<QuestState> ReachedStates => _reached;

        /// <summary>本任务的全部状态（供存档遍历分支/任务进度）。顺序不保证。</summary>
        internal IReadOnlyCollection<QuestState> AllStates => _statesById.Values;

        public bool IsInProgress => Completion == EQuestCompletion.Started;
        public bool IsSucceeded => Completion == EQuestCompletion.Succeeded;
        public bool IsFailed => Completion == EQuestCompletion.Failed;
        public bool IsFinished => IsSucceeded || IsFailed;

        // ---------------- 事件（对应 UE TalesComponent 的 OnQuest* 委托）----------------

        public event Action<Quest> Started;
        public event Action<Quest, QuestState> NewState;
        public event Action<Quest, QuestBranch, QuestTask, int, int> TaskProgressChanged; // (quest, branch, task, old, new)
        public event Action<Quest, QuestBranch, QuestTask> TaskCompleted;
        public event Action<Quest, QuestBranch> BranchCompleted;
        public event Action<Quest> Succeeded;
        public event Action<Quest> Failed;

        /// <summary>按 ID 取状态，找不到返回 null。</summary>
        public QuestState GetState(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return _statesById.TryGetValue(id, out var state) ? state : null;
        }

        /// <summary>
        /// 开始任务：进入起始状态（或指定的 <paramref name="startFromId"/>）。对应 UE BeginQuest。
        /// </summary>
        public bool Begin(string startFromId = null)
        {
            if (Completion != EQuestCompletion.NotStarted)
            {
                return false;
            }

            var start = !string.IsNullOrEmpty(startFromId) ? GetState(startFromId) : GetState(_startStateId);
            if (start == null)
            {
                return false;
            }

            Completion = EQuestCompletion.Started;
            Started?.Invoke(this);
            EnterState(start);
            return true;
        }

        // ---------------- 状态机核心 ----------------

        internal void EnterState(QuestState newState)
        {
            if (newState == null)
            {
                return;
            }

            // 先停用旧状态（结束其任务），再切到新状态。对应 UE EnterState_Internal。
            CurrentState?.Deactivate();
            CurrentState = newState;
            _reached.Add(newState);

            NewState?.Invoke(this, newState);

            // 注：顺序上我们先发 NewState（“已到达此状态”），再发 Succeeded/Failed（“因此任务结束”）——
            // 比 UE 略调整，读起来更符合因果；行为一致。
            switch (newState.NodeType)
            {
                case EStateNodeType.Success:
                    Completion = EQuestCompletion.Succeeded;
                    Succeeded?.Invoke(this);
                    return;
                case EStateNodeType.Failure:
                    Completion = EQuestCompletion.Failed;
                    Failed?.Invoke(this);
                    return;
                default:
                    newState.Activate(this, _context); // 激活分支 → 对任务 BeginTask
                    break;
            }
        }

        internal void TakeBranch(QuestBranch branch)
        {
            if (branch == null)
            {
                return;
            }

            // 对齐 UE TakeBranch：先停用分支（结束其任务），广播分支完成，再进入目标状态。
            branch.Deactivate();
            BranchCompleted?.Invoke(this, branch);
            EnterState(GetState(branch.DestinationStateId));
        }

        internal void NotifyTaskProgress(QuestBranch branch, QuestTask task, int oldProgress, int newProgress)
        {
            TaskProgressChanged?.Invoke(this, branch, task, oldProgress, newProgress);
        }

        internal void NotifyTaskCompleted(QuestBranch branch, QuestTask task)
        {
            TaskCompleted?.Invoke(this, branch, task);
        }

        /// <summary>
        /// 停用任务：结束当前状态的分支任务（让任务解绑宿主事件等），供宿主遗忘/重启时清理。
        /// 对应 UE <c>UQuest::Deinitialize</c>。不广播任何 quest 事件。
        /// </summary>
        internal void Deinitialize()
        {
            CurrentState?.Deactivate();
        }

        /// <summary>
        /// 读档用：清空到达过的状态、按存档的 ID 列表重填（跳过找不到的 ID）。不广播任何事件。
        /// 对应 UE PerformLoad 里“清空 ReachedStates 再按 ReachedStateNames 重建”。
        /// </summary>
        internal void RestoreReachedStates(IEnumerable<string> reachedStateIds)
        {
            _reached.Clear();
            if (reachedStateIds == null)
            {
                return;
            }

            foreach (var id in reachedStateIds)
            {
                var state = GetState(id);
                if (state != null)
                {
                    _reached.Add(state);
                }
            }
        }
    }
}
