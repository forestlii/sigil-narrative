// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务轮询驱动器：每帧把 Time.deltaTime 喂给宿主，驱动 TickInterval > 0 的任务轮询。
// 对应 UE 由每个任务各自的 TimerManager 定时器驱动 TickTask——这里集中成一个可选的 MonoBehaviour。
// 只在你用到“轮询型任务”（如“待在某区域 3 秒”）时才需要挂它；纯事件驱动的任务（data-task/对话）不需要。
// 逻辑本体在 NarrativeComponent.TickActiveTasks（可脱离 MonoBehaviour 单测），本类只是薄薄的每帧转发。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 每帧驱动 <see cref="NarrativeComponent.TickActiveTasks"/> 的可选组件。
    /// 挂到与 <see cref="NarrativeComponent"/> 同一个（或指定的）GameObject 上即可。
    /// </summary>
    [AddComponentMenu("Sigil/Narrative/Narrative Runner")]
    public sealed class NarrativeRunner : MonoBehaviour
    {
        [Tooltip("要驱动的叙事宿主。留空则在同一 GameObject 上自动查找。")]
        [SerializeField] private NarrativeComponent narrative;

        /// <summary>当前驱动的宿主。</summary>
        public NarrativeComponent Narrative
        {
            get => narrative;
            set => narrative = value;
        }

        private void Awake()
        {
            if (narrative == null)
            {
                narrative = GetComponent<NarrativeComponent>();
            }
        }

        private void Update()
        {
            if (narrative != null)
            {
                narrative.TickActiveTasks(Time.deltaTime);
            }
        }
    }
}
