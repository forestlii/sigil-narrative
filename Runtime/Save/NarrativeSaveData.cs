// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事存档的数据传输对象（DTO）。对应 UE 的 FNarrativeSavedQuest / FSavedQuestBranch + MasterTaskList。
// 刻意用 [Serializable] 的扁平字段 + List<>，让 Unity JsonUtility 能直接往返（不含逻辑、不引用运行时对象）。
// 只覆盖“路径一：叙事状态”——quest 进度 + 完成过的 data-task。world-actor 通用存档是后续里程碑。

using System;
using System.Collections.Generic;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一份叙事存档：玩家参与过的任务 + 完成过的 data-task 记录。
    /// 用 <see cref="NarrativeSaveManager"/> 与 JSON 互转；由宿主 Capture/Restore 生成与消费。
    /// 对应 UE 叙事宿主组件存档的 <c>SavedQuests</c> + <c>MasterTaskList</c>。
    /// </summary>
    [Serializable]
    public sealed class NarrativeSaveData
    {
        /// <summary>玩家参与过的任务（进行中/已成/已败），保存顺序即先后顺序。</summary>
        public List<SavedQuest> quests = new List<SavedQuest>();

        /// <summary>完成过的 data-task（原始串 → 次数），拆成条目列表以便 JsonUtility 序列化。</summary>
        public List<SavedMasterTask> masterTasks = new List<SavedMasterTask>();
    }

    /// <summary>一个任务的存档。对应 UE <c>FNarrativeSavedQuest</c>。</summary>
    [Serializable]
    public sealed class SavedQuest
    {
        /// <summary>任务资产的稳定身份（<see cref="QuestAsset.QuestId"/>）。读档按它找回资产。</summary>
        public string questId;

        /// <summary>存档时所处的状态 ID。读档从此状态恢复。对应 UE CurrentStateID。</summary>
        public string currentStateId;

        /// <summary>各分支的任务进度（按分支 ID 匹配，按任务顺序存进度）。对应 UE QuestBranches。</summary>
        public List<SavedQuestBranch> branches = new List<SavedQuestBranch>();

        /// <summary>到达过的状态 ID（按顺序）。对应 UE ReachedStateNames。</summary>
        public List<string> reachedStateIds = new List<string>();
    }

    /// <summary>一个分支的任务进度存档。对应 UE <c>FSavedQuestBranch</c>。</summary>
    [Serializable]
    public sealed class SavedQuestBranch
    {
        /// <summary>分支 ID（读档按它定位分支）。</summary>
        public string branchId;

        /// <summary>该分支各任务的当前进度，顺序与分支的任务顺序一致。对应 UE TasksProgress。</summary>
        public List<int> taskProgress = new List<int>();

        public SavedQuestBranch() { }

        public SavedQuestBranch(string branchId, List<int> taskProgress)
        {
            this.branchId = branchId;
            this.taskProgress = taskProgress ?? new List<int>();
        }
    }

    /// <summary>一条已完成 data-task 记录（Dictionary 无法被 JsonUtility 序列化，故拆成条目）。</summary>
    [Serializable]
    public sealed class SavedMasterTask
    {
        /// <summary>规范化后的原始任务串，如 "talkto_elder"。</summary>
        public string task;

        /// <summary>完成次数。</summary>
        public int count;

        public SavedMasterTask() { }

        public SavedMasterTask(string task, int count)
        {
            this.task = task;
            this.count = count;
        }
    }
}
