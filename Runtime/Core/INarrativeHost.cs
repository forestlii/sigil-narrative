// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事宿主接口。对应 UE 叙事宿主组件对外能力的一个精简契约。
// 用接口而非基类，条件/事件与运行时逻辑只依赖它，不依赖具体 MonoBehaviour——既解耦又便于测试与替身。

using System;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事宿主对外契约：条件/事件运行时通过它查询与改写叙事状态。
    /// 具体实现是 <see cref="NarrativeComponent"/>（MonoBehaviour）。
    /// data-task 面 + 任务（quest）开始/查询——内置的任务相关条件/事件（如
    /// <see cref="BeginQuestEvent"/>、<see cref="QuestStateCondition"/>）只依赖本接口，
    /// 从而可用测试替身，不绑定具体 MonoBehaviour。
    /// </summary>
    public interface INarrativeHost
    {
        // ---------------- data-task ----------------

        /// <summary>玩家“做过什么”的持久记录（data-task 完成次数）。</summary>
        MasterTaskList MasterTasks { get; }

        /// <summary>data-task 完成时触发，携带规范化后的原始任务串。对应 UE OnNarrativeDataTaskCompleted。</summary>
        event Action<string> DataTaskCompleted;

        /// <summary>用一个 <see cref="DataTaskDefinition"/> + 参数记录完成。返回是否成功记录。</summary>
        bool CompleteDataTask(DataTaskDefinition task, string argument, int quantity = 1);

        /// <summary>用任务名 + 参数记录完成（免资产版）。对应 UE CompleteNarrativeDataTask 的字符串重载。</summary>
        bool CompleteDataTask(string taskName, string argument, int quantity = 1);

        // ---------------- 任务（quest）：内置条件/事件所需的最小面 ----------------

        /// <summary>
        /// 开始一个任务（从资产克隆干净实例并进入起始/指定状态）。已在进行则返回 <c>null</c>。
        /// 对应 UE <c>BeginQuest</c>。供 <see cref="BeginQuestEvent"/> 使用。
        /// </summary>
        Quest BeginQuest(QuestAsset quest, string startFromId = null);

        /// <summary>任务是否进行中（不含已结束）。对应 UE <c>IsQuestInProgress</c>。</summary>
        bool IsQuestInProgress(QuestAsset quest);

        /// <summary>任务是否已成功。对应 UE <c>IsQuestSucceeded</c>。</summary>
        bool IsQuestSucceeded(QuestAsset quest);

        /// <summary>任务是否已失败。对应 UE <c>IsQuestFailed</c>。</summary>
        bool IsQuestFailed(QuestAsset quest);

        /// <summary>任务是否已结束（成功或失败）。对应 UE <c>IsQuestFinished</c>。</summary>
        bool IsQuestFinished(QuestAsset quest);

        /// <summary>任务是否已开始或已结束（即已参与过）。对应 UE <c>IsQuestStartedOrFinished</c>。</summary>
        bool IsQuestStartedOrFinished(QuestAsset quest);
    }
}
