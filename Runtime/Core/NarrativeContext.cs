// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 条件/事件运行时的上下文。对应 UE 里到处传的 (APawn* Target, APlayerController*, 叙事宿主组件*) 三元组。
// 单机 + 无角色系统，精简为“宿主 + 可选目标 GameObject”。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 传给条件/事件的运行时上下文。替代 UE 的 (Target, Controller, NarrativeComponent) 三参数。
    /// <see cref="Host"/> 是必需的；<see cref="Target"/> 可选（未来给需要作用于具体角色的条件/事件用，M1 一般不需要）。
    /// </summary>
    public sealed class NarrativeContext
    {
        /// <summary>叙事宿主（必需）。条件/事件通过它查询/改写叙事状态。</summary>
        public INarrativeHost Host { get; }

        /// <summary>可选的目标对象（这条对话/任务作用于谁）。M1 通常为空。</summary>
        public GameObject Target { get; }

        public NarrativeContext(INarrativeHost host, GameObject target = null)
        {
            Host = host;
            Target = target;
        }
    }
}
