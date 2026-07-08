// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务状态查询种类。供 QuestStateCondition 选择要检查任务的哪种状态。

namespace Likeon.Narrative
{
    /// <summary>
    /// <see cref="QuestStateCondition"/> 要检查的任务状态种类。对应宿主上的一组 IsQuest* 查询。
    /// </summary>
    public enum EQuestStateQuery
    {
        /// <summary>进行中（不含已结束）。</summary>
        InProgress,

        /// <summary>已成功。</summary>
        Succeeded,

        /// <summary>已失败。</summary>
        Failed,

        /// <summary>已结束（成功或失败）。</summary>
        Finished,

        /// <summary>已开始或已结束（即已参与过）。</summary>
        StartedOrFinished,
    }
}
