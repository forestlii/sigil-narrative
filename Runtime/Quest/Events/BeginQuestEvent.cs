// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 内置事件：到达节点时在宿主上开始一个任务。对应 UE 里最常见的 NarrativeEvent 用法——
// “和长老对话/到达某状态即开启任务 X”。默认 RefireOnLoad=false：开任务是一次性副作用，读档不重开。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 事件：执行时调 <see cref="INarrativeHost.BeginQuest"/> 开始指定任务。
    /// 常挂在对话行或任务状态的进入事件上，把叙事推进串起来。
    /// 只依赖 <see cref="INarrativeHost"/>。默认 <see cref="NarrativeEvent.RefireOnLoad"/> 为 <c>false</c>
    /// （已在进行的任务再 BeginQuest 会被宿主忽略并告警，读档时不应重触发）。
    /// </summary>
    [System.Serializable]
    public sealed class BeginQuestEvent : NarrativeEvent
    {
        [Tooltip("要开始的任务资产。")]
        [SerializeField] private QuestAsset quest;

        [Tooltip("可选：从此状态 ID 开始（留空=起始状态）。")]
        [SerializeField] private string startFromStateId;

        public BeginQuestEvent()
        {
            // 开任务是一次性副作用：读档不重开（对齐“给奖励”类事件的语义）。
            RefireOnLoad = false;
        }

        public BeginQuestEvent(QuestAsset quest, string startFromStateId = null) : this()
        {
            this.quest = quest;
            this.startFromStateId = startFromStateId;
        }

        /// <summary>要开始的任务资产。</summary>
        public QuestAsset Quest => quest;

        public override void Execute(NarrativeContext context)
        {
            if (quest == null)
            {
                return;
            }

            context?.Host?.BeginQuest(quest, string.IsNullOrEmpty(startFromStateId) ? null : startFromStateId);
        }

        public override string GetDisplayText()
        {
            return quest != null ? $"Begin quest \"{quest.QuestId}\"" : "Begin <quest>";
        }

        public override string GetHintText() => "(Begin Quest)";
    }
}
