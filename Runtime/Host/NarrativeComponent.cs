// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事宿主组件。对应 UE 的 UTalesComponent（挂在 PlayerController 上）。
// 这里做成 MonoBehaviour + 实现 INarrativeHost——不强制用户继承任何基类，加到任意 GameObject 上即可。
// M1 只覆盖 data-task 面（记录/查询/事件）；对话、任务、存档随后续里程碑扩充。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事系统的宿主组件——开始对话/任务、完成 data-task、查询叙事状态的入口。
    /// 对应 UE <c>UTalesComponent</c>。加到你的玩家 GameObject 上即可，无需继承基类。
    /// </summary>
    [AddComponentMenu("Sigil/Narrative/Narrative Component")]
    public class NarrativeComponent : MonoBehaviour, INarrativeHost
    {
        private readonly MasterTaskList _masterTasks = new MasterTaskList();

        // 玩家参与的所有任务（进行中/已成/已败）。对应 UE UTalesComponent::QuestList。
        private readonly List<Quest> _questList = new List<Quest>();

        // 每个 quest 实例的事件解绑器，Forget/Restart 时用来断开桥接，避免悬空订阅。
        private readonly Dictionary<Quest, Action> _questUnsubscribe = new Dictionary<Quest, Action>();

        /// <inheritdoc/>
        public MasterTaskList MasterTasks => _masterTasks;

        /// <inheritdoc/>
        public event Action<string> DataTaskCompleted;

        /// <inheritdoc/>
        public bool CompleteDataTask(DataTaskDefinition task, string argument, int quantity = 1)
        {
            if (task == null)
            {
                return false;
            }

            return CompleteRawTask(task.MakeTaskString(argument), quantity);
        }

        /// <inheritdoc/>
        public bool CompleteDataTask(string taskName, string argument, int quantity = 1)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return false;
            }

            return CompleteRawTask(DataTaskDefinition.Normalize(taskName, argument), quantity);
        }

        private bool CompleteRawTask(string rawTaskString, int quantity)
        {
            if (string.IsNullOrEmpty(rawTaskString))
            {
                return false;
            }

            _masterTasks.CompleteTask(rawTaskString, quantity);
            DataTaskCompleted?.Invoke(rawTaskString);
            return true;
        }

        // ==================== 任务（Quest）====================
        //
        // 身份键 = QuestAsset（对应 UE 的 QuestClass）：QuestList 里每个 asset 最多一个实例。
        // 宿主把每个运行中 quest 的事件桥接到下面的 OnQuest* 宿主事件，供 UI/存档统一监听。

        /// <summary>任务开始。对应 UE OnQuestStarted。</summary>
        public event Action<Quest> QuestStarted;

        /// <summary>任务被遗忘（从任务列表移除）。对应 UE OnQuestForgotten。</summary>
        public event Action<Quest> QuestForgotten;

        /// <summary>任务被重启。携带的是被替换掉的旧实例。对应 UE OnQuestRestarted。</summary>
        public event Action<Quest> QuestRestarted;

        /// <summary>任务到达新状态。对应 UE OnQuestNewState。</summary>
        public event Action<Quest, QuestState> QuestNewState;

        /// <summary>分支完成（取该分支）。对应 UE OnQuestBranchCompleted。</summary>
        public event Action<Quest, QuestBranch> QuestBranchCompleted;

        /// <summary>任务项进度变化 (quest, branch, task, old, new)。对应 UE OnQuestTaskProgressChanged。</summary>
        public event Action<Quest, QuestBranch, QuestTask, int, int> QuestTaskProgressChanged;

        /// <summary>任务项完成。对应 UE OnQuestTaskCompleted。</summary>
        public event Action<Quest, QuestBranch, QuestTask> QuestTaskCompleted;

        /// <summary>任务成功。对应 UE OnQuestSucceeded。</summary>
        public event Action<Quest> QuestSucceeded;

        /// <summary>任务失败。对应 UE OnQuestFailed。</summary>
        public event Action<Quest> QuestFailed;

        /// <summary>玩家参与的所有任务（进行中/已成/已败），按开始的先后顺序。对应 UE GetAllQuests。</summary>
        public IReadOnlyList<Quest> AllQuests => _questList;

        /// <summary>
        /// 开始一个任务：从资产克隆出干净实例、加入任务列表、桥接事件、进入起始（或指定）状态。
        /// 对应 UE <c>BeginQuest</c>。若该资产已在任务列表中（进行中或已结束），警告并返回 <c>null</c>——
        /// 想重玩请用 <see cref="RestartQuest"/>。
        /// </summary>
        /// <param name="quest">任务资产模板。</param>
        /// <param name="startFromId">可选：跳过起始状态、从此状态 ID 开始。</param>
        /// <returns>新建的运行时任务实例；失败返回 <c>null</c>。</returns>
        public Quest BeginQuest(QuestAsset quest, string startFromId = null)
        {
            if (quest == null)
            {
                return null;
            }

            if (GetQuestInstance(quest) != null)
            {
                Debug.LogWarning($"[Narrative] BeginQuest 被要求开始一个已在进行的任务：{quest.name}。想重玩请用 RestartQuest()。");
                return null;
            }

            var instance = quest.CreateRuntimeQuest(MakeContext());
            _questList.Add(instance);
            WireQuest(instance);   // 先桥接再 Begin，让 Started/NewState 也能广播出去
            instance.Begin(startFromId);
            return instance;
        }

        /// <summary>
        /// 重启一个任务：仅当该资产已在任务列表中才生效。广播 <see cref="QuestRestarted"/>（携带旧实例）后，
        /// 移除旧实例并以全新实例重新开始。对应 UE <c>RestartQuest</c>。
        /// </summary>
        /// <returns>是否成功重启（未开始过则 <c>false</c>）。</returns>
        public bool RestartQuest(QuestAsset quest, string startFromId = null)
        {
            if (quest == null)
            {
                return false;
            }

            var existing = GetQuestInstance(quest);
            if (existing == null)
            {
                return false;
            }

            QuestRestarted?.Invoke(existing);
            RemoveQuestInstance(existing);
            BeginQuest(quest, startFromId);
            return true;
        }

        /// <summary>
        /// 遗忘一个任务：从任务列表移除，之后可再次 <see cref="BeginQuest"/>。对应 UE <c>ForgetQuest</c>。
        /// </summary>
        /// <returns>是否成功遗忘（不在列表中则 <c>false</c>）。</returns>
        public bool ForgetQuest(QuestAsset quest)
        {
            if (quest == null)
            {
                return false;
            }

            var existing = GetQuestInstance(quest);
            if (existing == null)
            {
                return false;
            }

            RemoveQuestInstance(existing);
            QuestForgotten?.Invoke(existing);
            return true;
        }

        /// <summary>按资产取运行中的任务实例，未开始则 <c>null</c>。对应 UE <c>GetQuestInstance</c>。</summary>
        public Quest GetQuestInstance(QuestAsset quest)
        {
            if (quest == null)
            {
                return null;
            }

            foreach (var q in _questList)
            {
                if (q != null && q.SourceAsset == quest)
                {
                    return q;
                }
            }

            return null;
        }

        /// <summary>任务是否已开始或已结束（即在任务列表中且非 NotStarted）。对应 UE <c>IsQuestStartedOrFinished</c>。</summary>
        public bool IsQuestStartedOrFinished(QuestAsset quest)
        {
            var q = GetQuestInstance(quest);
            return q != null && q.Completion != EQuestCompletion.NotStarted;
        }

        /// <summary>任务是否进行中（不含已结束）。对应 UE <c>IsQuestInProgress</c>。</summary>
        public bool IsQuestInProgress(QuestAsset quest)
        {
            var q = GetQuestInstance(quest);
            return q != null && q.IsInProgress;
        }

        /// <summary>任务是否已成功。对应 UE <c>IsQuestSucceeded</c>。</summary>
        public bool IsQuestSucceeded(QuestAsset quest)
        {
            var q = GetQuestInstance(quest);
            return q != null && q.IsSucceeded;
        }

        /// <summary>任务是否已失败。对应 UE <c>IsQuestFailed</c>。</summary>
        public bool IsQuestFailed(QuestAsset quest)
        {
            var q = GetQuestInstance(quest);
            return q != null && q.IsFailed;
        }

        /// <summary>任务是否已结束（成功或失败）。对应 UE <c>IsQuestFinished</c>。</summary>
        public bool IsQuestFinished(QuestAsset quest)
        {
            var q = GetQuestInstance(quest);
            return q != null && q.IsFinished;
        }

        /// <summary>所有进行中的任务，按先后顺序。对应 UE <c>GetInProgressQuests</c>。</summary>
        public List<Quest> GetInProgressQuests() => FilterQuests(q => q.IsInProgress);

        /// <summary>所有已成功的任务，按先后顺序。对应 UE <c>GetSucceededQuests</c>。</summary>
        public List<Quest> GetSucceededQuests() => FilterQuests(q => q.IsSucceeded);

        /// <summary>所有已失败的任务，按先后顺序。对应 UE <c>GetFailedQuests</c>。</summary>
        public List<Quest> GetFailedQuests() => FilterQuests(q => q.IsFailed);

        private List<Quest> FilterQuests(Func<Quest, bool> predicate)
        {
            var result = new List<Quest>();
            foreach (var q in _questList)
            {
                if (q != null && predicate(q))
                {
                    result.Add(q);
                }
            }

            return result;
        }

        // 从列表移除：先停用（结束任务、解绑宿主 data-task 订阅），再断开事件桥接。
        private void RemoveQuestInstance(Quest quest)
        {
            quest?.Deinitialize();
            UnwireQuest(quest);
            _questList.Remove(quest);
        }

        // 把一个 quest 实例的事件桥接到宿主 OnQuest* 事件，并登记对应的解绑动作。
        private void WireQuest(Quest quest)
        {
            Action<Quest> started = q => QuestStarted?.Invoke(q);
            Action<Quest, QuestState> newState = (q, s) => QuestNewState?.Invoke(q, s);
            Action<Quest, QuestBranch, QuestTask, int, int> progress = (q, b, t, o, n) => QuestTaskProgressChanged?.Invoke(q, b, t, o, n);
            Action<Quest, QuestBranch, QuestTask> taskCompleted = (q, b, t) => QuestTaskCompleted?.Invoke(q, b, t);
            Action<Quest, QuestBranch> branchCompleted = (q, b) => QuestBranchCompleted?.Invoke(q, b);
            Action<Quest> succeeded = q => QuestSucceeded?.Invoke(q);
            Action<Quest> failed = q => QuestFailed?.Invoke(q);

            quest.Started += started;
            quest.NewState += newState;
            quest.TaskProgressChanged += progress;
            quest.TaskCompleted += taskCompleted;
            quest.BranchCompleted += branchCompleted;
            quest.Succeeded += succeeded;
            quest.Failed += failed;

            _questUnsubscribe[quest] = () =>
            {
                quest.Started -= started;
                quest.NewState -= newState;
                quest.TaskProgressChanged -= progress;
                quest.TaskCompleted -= taskCompleted;
                quest.BranchCompleted -= branchCompleted;
                quest.Succeeded -= succeeded;
                quest.Failed -= failed;
            };
        }

        private void UnwireQuest(Quest quest)
        {
            if (quest != null && _questUnsubscribe.TryGetValue(quest, out var unsubscribe))
            {
                unsubscribe();
                _questUnsubscribe.Remove(quest);
            }
        }

        /// <summary>构造一个以本宿主为根的 <see cref="NarrativeContext"/>。</summary>
        public NarrativeContext MakeContext(GameObject target = null)
        {
            return new NarrativeContext(this, target);
        }
    }
}
