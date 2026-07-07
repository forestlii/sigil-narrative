// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 一条对话行。对应 UE 的 FDialogueLine（这里落成 [Serializable] class）。
// 只保留逻辑相关字段（文本/时长/条件）；音频/动画/镜头等表现绑定由 presenter 或资产层承载，不进核心。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一条对话行。对应 UE <c>FDialogueLine</c> 的逻辑子集：文本、时长模式、可选条件。
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        [Tooltip("本行文本。对应 UE Text。")]
        [TextArea, SerializeField] private string text;

        [Tooltip("本行的时长模式。对应 UE Duration。")]
        [SerializeField] private ELineDuration duration = ELineDuration.Default;

        [Tooltip("固定秒数（仅 AfterDuration 用）。对应 UE DurationSecondsOverride。")]
        [SerializeField] private float durationSecondsOverride = 0f;

        [Tooltip("可选：本行需满足这些条件才会被选中。对应 UE FDialogueLine.Conditions。")]
        [SerializeReference] private List<NarrativeCondition> conditions = new List<NarrativeCondition>();

        public DialogueLine() { }

        public DialogueLine(string text, ELineDuration duration = ELineDuration.Default)
        {
            this.text = text;
            this.duration = duration;
        }

        /// <summary>本行文本。</summary>
        public string Text
        {
            get => text ?? string.Empty;
            set => text = value;
        }

        /// <summary>时长模式。</summary>
        public ELineDuration Duration
        {
            get => duration;
            set => duration = value;
        }

        /// <summary>固定秒数（仅 <see cref="ELineDuration.AfterDuration"/> 用）。</summary>
        public float DurationSecondsOverride
        {
            get => durationSecondsOverride;
            set => durationSecondsOverride = value;
        }

        /// <summary>本行的选中条件。</summary>
        public IReadOnlyList<NarrativeCondition> Conditions => conditions;

        /// <summary>追加一个条件（代码构建用；null 忽略）。</summary>
        public void AddCondition(NarrativeCondition condition)
        {
            if (condition != null)
            {
                (conditions ??= new List<NarrativeCondition>()).Add(condition);
            }
        }

        /// <summary>本行的所有条件是否满足（空列表视为满足）。</summary>
        public bool AreConditionsMet(NarrativeContext context)
        {
            if (conditions == null)
            {
                return true;
            }

            foreach (var condition in conditions)
            {
                if (condition != null && !condition.IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 阅读时长（秒）：字数 / 每秒字数，且不小于最小显示时长。空文本返回 0。
        /// 对应 UE 用 LettersPerSecondLineDuration + MinDialogueTextDisplayTime 的算法。
        /// </summary>
        public float GetReadingTime(float lettersPerSecond, float minDisplayTime)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            if (lettersPerSecond <= 0f)
            {
                return minDisplayTime;
            }

            return Mathf.Max(text.Length / lettersPerSecond, minDisplayTime);
        }

        /// <summary>
        /// 把 <see cref="ELineDuration.Default"/> 解析成一个具体模式（其余模式原样返回）。
        /// 音频/序列是否存在由表现层告知——对应 UE Default 的回退顺序：
        /// 有音频→WhenAudioEnds；有序列且无文本→WhenSequenceEnds；否则→AfterReadingTime(空文本即 0 秒瞬结)。
        /// </summary>
        public ELineDuration ResolveEffectiveDuration(bool hasAudio, bool hasSequence)
        {
            if (duration != ELineDuration.Default)
            {
                return duration;
            }

            if (hasAudio)
            {
                return ELineDuration.WhenAudioEnds;
            }

            if (hasSequence && string.IsNullOrEmpty(text))
            {
                return ELineDuration.WhenSequenceEnds;
            }

            return ELineDuration.AfterReadingTime;
        }
    }
}
