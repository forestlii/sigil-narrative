// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对话表现层接口。核心只负责“谁该说什么、有哪些选项”的逻辑推进；
// “怎么显示、播多久、镜头怎么走”交给宿主实现本接口（可用 UGUI/Timeline/Cinemachine 等）。
// 这是把 UE UDialogue 里那一大堆表现代码剥出去的落点。

using System.Collections.Generic;

namespace Likeon.Narrative
{
    /// <summary>
    /// 对话表现层契约。<see cref="DialogueController"/> 只推进逻辑并回调本接口；
    /// 表现方决定何时“这一行播完了”，届时调用 <see cref="DialogueController.AdvanceLine"/> 推进。
    /// </summary>
    public interface IDialoguePresenter
    {
        /// <summary>对话开始。</summary>
        void OnDialogueBegan(DialogueController controller);

        /// <summary>一行开始播放（NPC 台词或玩家选中的台词）。表现方据 <paramref name="line"/> 显示并计时。</summary>
        void OnLineStarted(DialogueNode node, DialogueLine line);

        /// <summary>一行播放结束（在表现方调用 AdvanceLine 之后回调）。</summary>
        void OnLineFinished(DialogueNode node, DialogueLine line);

        /// <summary>NPC 说完当前 chunk，轮到玩家：给出可选回复列表，等待 SelectOption。</summary>
        void OnResponsesAvailable(IReadOnlyList<DialogueNode_Player> options);

        /// <summary>对话结束。</summary>
        void OnDialogueEnded(DialogueController controller);
    }
}
