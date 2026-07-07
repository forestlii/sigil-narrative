// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务的完成状态。对应 UE 的 EQuestCompletion。

namespace Likeon.Narrative
{
    /// <summary>
    /// 一个任务当前的完成状态。对应 UE <c>EQuestCompletion</c>。
    /// </summary>
    public enum EQuestCompletion
    {
        /// <summary>尚未开始。</summary>
        NotStarted,

        /// <summary>已开始、进行中。</summary>
        Started,

        /// <summary>已成功完成。</summary>
        Succeeded,

        /// <summary>已失败。</summary>
        Failed,
    }
}
