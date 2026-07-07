// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务状态节点的类型。对应 UE 的 EStateNodeType。

namespace Likeon.Narrative
{
    /// <summary>
    /// 任务状态机里一个状态节点的类型。对应 UE <c>EStateNodeType</c>。
    /// </summary>
    public enum EStateNodeType
    {
        /// <summary>进行中：到达此状态任务仍在进行。</summary>
        Regular,

        /// <summary>成功：到达此状态任务判为成功并结束。</summary>
        Success,

        /// <summary>失败：到达此状态任务判为失败并结束。</summary>
        Failure,
    }
}
