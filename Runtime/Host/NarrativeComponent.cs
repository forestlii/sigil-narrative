// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事宿主组件。对应 UE 里挂在 PlayerController 上的叙事宿主组件。
// 这里做成 MonoBehaviour + 实现 INarrativeHost——不强制用户继承任何基类，加到任意 GameObject 上即可。
// M1 只覆盖 data-task 面（记录/查询/事件）；对话、任务、存档随后续里程碑扩充。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事系统的宿主组件——开始对话/任务、完成 data-task、查询叙事状态的入口。
    /// 对应 UE 的叙事宿主组件。加到你的玩家 GameObject 上即可，无需继承基类。
    /// </summary>
    [AddComponentMenu("Sigil/Narrative/Narrative Component")]
    public class NarrativeComponent : MonoBehaviour, INarrativeHost
    {
        private readonly MasterTaskList _masterTasks = new MasterTaskList();

        // 玩家参与的所有任务（进行中/已成/已败）。对应 UE 叙事宿主组件的 QuestList。
        private readonly List<Quest> _questList = new List<Quest>();

        // 每个 quest 实例的事件解绑器，Forget/Restart 时用来断开桥接，避免悬空订阅。
        private readonly Dictionary<Quest, Action> _questUnsubscribe = new Dictionary<Quest, Action>();

        // 读档期间为 true：此时不向外广播任何宿主级 OnQuest* 事件（对应 UE 的 bIsLoading）。
        private bool _isLoading;

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
            return BeginQuestInternal(quest, startFromId, false);
        }

        // loading=true 时走 BeginForLoad：状态进入事件按 RefireOnLoad 过滤（读档路径用）。
        private Quest BeginQuestInternal(QuestAsset quest, string startFromId, bool loading)
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

            if (loading)
            {
                instance.BeginForLoad(startFromId);
            }
            else
            {
                instance.Begin(startFromId);
            }

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

        /// <summary>
        /// 驱动所有进行中任务的当前激活任务轮询（<see cref="QuestTask.TickInterval"/> &gt; 0 的任务）。
        /// 每帧由 <see cref="NarrativeRunner"/> 用 <c>Time.deltaTime</c> 调用；也可手动调（便于测试）。
        /// 对应 UE 由 TimerManager 分散驱动的 TickTask，这里集中到宿主一处。
        /// </summary>
        public void TickActiveTasks(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            // 快照，防 tick 引发 quest 结束/移除时改动列表。
            var snapshot = new List<Quest>(_questList);
            foreach (var quest in snapshot)
            {
                if (quest != null && quest.IsInProgress)
                {
                    quest.TickActiveTasks(deltaSeconds);
                }
            }
        }

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
        // 读档期间（_isLoading）一律不广播，避免读档重建时把一堆 Started/NewState 假事件推给 UI。
        private void WireQuest(Quest quest)
        {
            Action<Quest> started = q => { if (!_isLoading) QuestStarted?.Invoke(q); };
            Action<Quest, QuestState> newState = (q, s) => { if (!_isLoading) QuestNewState?.Invoke(q, s); };
            Action<Quest, QuestBranch, QuestTask, int, int> progress = (q, b, t, o, n) => { if (!_isLoading) QuestTaskProgressChanged?.Invoke(q, b, t, o, n); };
            Action<Quest, QuestBranch, QuestTask> taskCompleted = (q, b, t) => { if (!_isLoading) QuestTaskCompleted?.Invoke(q, b, t); };
            Action<Quest, QuestBranch> branchCompleted = (q, b) => { if (!_isLoading) QuestBranchCompleted?.Invoke(q, b); };
            Action<Quest> succeeded = q => { if (!_isLoading) QuestSucceeded?.Invoke(q); };
            Action<Quest> failed = q => { if (!_isLoading) QuestFailed?.Invoke(q); };

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

        // ==================== 存档 / 读档（叙事状态）====================
        //
        // 只覆盖“路径一：叙事状态” = 任务进度 + 完成过的 data-task。world-actor 通用存档是后续里程碑。
        // 对应 UE 叙事宿主组件的 PrepareForSave / PerformLoad。

        /// <summary>是否正在读档（此间不广播 OnQuest* 事件）。对应 UE bIsLoading。</summary>
        public bool IsLoading => _isLoading;

        /// <summary>
        /// 把当前叙事状态快照成一份 <see cref="NarrativeSaveData"/>：每个任务的所处状态、各分支任务进度、
        /// 到达过的状态，以及完成过的 data-task 表。对应 UE <c>PrepareForSave</c>。
        /// </summary>
        public NarrativeSaveData CaptureNarrativeState()
        {
            var data = new NarrativeSaveData();

            foreach (var entry in _masterTasks.Entries)
            {
                data.masterTasks.Add(new SavedMasterTask(entry.Key, entry.Value));
            }

            foreach (var quest in _questList)
            {
                if (quest == null || quest.SourceAsset == null || quest.CurrentState == null)
                {
                    continue;
                }

                var saved = new SavedQuest
                {
                    questId = quest.SourceAsset.QuestId,
                    currentStateId = quest.CurrentState.Id,
                };

                // 存所有分支的任务进度（对齐 UE：整套分支都存，未访问分支进度为 0）。
                foreach (var state in quest.AllStates)
                {
                    foreach (var branch in state.Branches)
                    {
                        if (branch == null)
                        {
                            continue;
                        }

                        var savedBranch = new SavedQuestBranch { branchId = branch.Id };
                        foreach (var task in branch.Tasks)
                        {
                            savedBranch.taskProgress.Add(task != null ? task.CurrentProgress : 0);
                        }

                        saved.branches.Add(savedBranch);
                    }
                }

                foreach (var reached in quest.ReachedStates)
                {
                    if (reached != null)
                    {
                        saved.reachedStateIds.Add(reached.Id);
                    }
                }

                data.quests.Add(saved);
            }

            return data;
        }

        /// <summary>
        /// 从一份存档还原叙事状态：清空当前任务与 data-task 表，按存档重建每个任务到其所处状态并回填进度。
        /// 读档全程 <see cref="IsLoading"/>=true，不广播任何 OnQuest* 事件。对应 UE <c>PerformLoad</c>。
        /// </summary>
        /// <param name="data">存档数据。</param>
        /// <param name="knownQuests">可能出现在存档里的全部任务资产（按 <see cref="QuestAsset.QuestId"/> 找回）。</param>
        /// <returns>是否成功应用（data 为 null 返回 false）。存档里找不到对应资产的任务会被跳过并警告。</returns>
        public bool RestoreNarrativeState(NarrativeSaveData data, IEnumerable<QuestAsset> knownQuests)
        {
            if (data == null)
            {
                return false;
            }

            // 建 questId → 资产 的查找表。
            var catalog = new Dictionary<string, QuestAsset>();
            if (knownQuests != null)
            {
                foreach (var asset in knownQuests)
                {
                    if (asset != null && !string.IsNullOrEmpty(asset.QuestId) && !catalog.ContainsKey(asset.QuestId))
                    {
                        catalog[asset.QuestId] = asset;
                    }
                }
            }

            _isLoading = true;
            try
            {
                // 1. data-task 表整表还原（RestoreEntry 不触发完成事件）。
                _masterTasks.Clear();
                if (data.masterTasks != null)
                {
                    foreach (var entry in data.masterTasks)
                    {
                        if (entry != null)
                        {
                            _masterTasks.RestoreEntry(entry.task, entry.count);
                        }
                    }
                }

                // 2. 遗忘所有当前任务（静默移除，不广播 Forgotten）。
                for (int i = _questList.Count - 1; i >= 0; i--)
                {
                    RemoveQuestInstance(_questList[i]);
                }

                // 3. 逐个重建存档里的任务。
                if (data.quests != null)
                {
                    foreach (var savedQuest in data.quests)
                    {
                        if (savedQuest == null || string.IsNullOrEmpty(savedQuest.questId))
                        {
                            continue;
                        }

                        if (!catalog.TryGetValue(savedQuest.questId, out var asset))
                        {
                            Debug.LogWarning($"[Narrative] 读档跳过未知任务 \"{savedQuest.questId}\"——不在 knownQuests 里。");
                            continue;
                        }

                        // 直接从存档所处状态开始（激活该状态的分支/任务，订阅宿主）；
                        // loading=true → 该状态的进入事件按 RefireOnLoad 过滤，一次性事件不重放。
                        var quest = BeginQuestInternal(asset, savedQuest.currentStateId, true);
                        if (quest == null)
                        {
                            continue;
                        }

                        RestoreBranchProgress(quest, savedQuest.branches);
                        quest.RestoreReachedStates(savedQuest.reachedStateIds);
                    }
                }
            }
            finally
            {
                _isLoading = false;
            }

            return true;
        }

        // 按分支 ID 把存档里的任务进度回填到重建后的任务上（RestoreProgress 不触发事件、不通知分支）。
        private static void RestoreBranchProgress(Quest quest, List<SavedQuestBranch> savedBranches)
        {
            if (savedBranches == null)
            {
                return;
            }

            foreach (var state in quest.AllStates)
            {
                foreach (var branch in state.Branches)
                {
                    if (branch == null)
                    {
                        continue;
                    }

                    foreach (var savedBranch in savedBranches)
                    {
                        if (savedBranch == null || branch.Id != savedBranch.branchId)
                        {
                            continue;
                        }

                        var tasks = branch.Tasks;
                        for (int i = 0; i < tasks.Count && i < savedBranch.taskProgress.Count; i++)
                        {
                            tasks[i]?.RestoreProgress(savedBranch.taskProgress[i]);
                        }
                    }
                }
            }
        }

        /// <summary>构造一个以本宿主为根的 <see cref="NarrativeContext"/>。</summary>
        public NarrativeContext MakeContext(GameObject target = null)
        {
            return new NarrativeContext(this, target);
        }
    }
}
