// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 事件触发时机。对应 UE 的 EEventRuntime。

namespace Likeon.Narrative
{
    /// <summary>
    /// 事件在节点的哪个阶段执行。对应 UE <c>EEventRuntime</c>。
    /// </summary>
    public enum EEventRuntime
    {
        /// <summary>进入时执行：对话行开始播放 / 任务状态进入 / 分支激活。</summary>
        Start,

        /// <summary>离开时执行：对话行播放结束 / 任务状态退出 / 分支被取用而停用。</summary>
        End,

        /// <summary>开始和结束都执行（会触发两次）。</summary>
        Both,
    }
}
