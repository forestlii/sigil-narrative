// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对话行何时结束、该播下一条。对应 UE 的 ELineDuration。

namespace Likeon.Narrative
{
    /// <summary>
    /// 对话行的时长模式。对应 UE <c>ELineDuration</c>。
    /// 依赖表现层的两档(WhenAudioEnds/WhenSequenceEnds)在核心里只作为枚举存在，具体结束时机由 presenter 决定。
    /// </summary>
    public enum ELineDuration
    {
        /// <summary>默认：有音频→等音频；有序列且无文本→等序列；有文本→按阅读时长；空行→瞬间结束。</summary>
        Default,

        /// <summary>音频播完时结束（表现层判定）。</summary>
        WhenAudioEnds,

        /// <summary>序列播完时结束（表现层判定）。</summary>
        WhenSequenceEnds,

        /// <summary>按阅读时长结束（字数 / 每秒字数，受最小显示时长约束）。</summary>
        AfterReadingTime,

        /// <summary>固定秒数后结束。</summary>
        AfterDuration,

        /// <summary>永不自动结束，只能靠跳过。</summary>
        Never,
    }
}
