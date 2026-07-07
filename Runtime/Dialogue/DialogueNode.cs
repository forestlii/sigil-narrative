// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对话节点基类。对应 UE 的 UDialogueNode（继承 UNarrativeNodeBase）。
// 持有一条 Line + 可选备选行、子边(按 ID 引用 NPC/玩家回复)、是否可跳过。
// 边用 string ID 而非对象引用——对齐“扁平运行时模板”设计（图由 DialogueGraph 按 ID 解析）。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 对话节点基类。对应 UE <c>UDialogueNode</c>。子类 <see cref="DialogueNode_NPC"/> / <see cref="DialogueNode_Player"/>。
    /// </summary>
    [Serializable]
    public class DialogueNode : NarrativeNodeBase
    {
        [Tooltip("本节点的主对话行。对应 UE Line。")]
        [SerializeField] private DialogueLine line = new DialogueLine();

        [Tooltip("备选行：narrative 会在主行与满足条件的备选行里随机选一条。对应 UE AlternativeLines。")]
        [SerializeField] private List<DialogueLine> alternativeLines = new List<DialogueLine>();

        [Tooltip("后续 NPC 回复节点的 ID（按 ID 引用）。对应 UE NPCReplies。")]
        [SerializeField] private List<string> npcReplyIds = new List<string>();

        [Tooltip("玩家回复选项节点的 ID（按 ID 引用）。对应 UE PlayerReplies。")]
        [SerializeField] private List<string> playerReplyIds = new List<string>();

        [Tooltip("这条线是说给哪个说话人听的，可空。对应 UE DirectedAtSpeakerID。")]
        [SerializeField] private string directedAtSpeakerId;

        [Tooltip("按跳过键是否可跳过本行。对应 UE bIsSkippable。")]
        [SerializeField] private bool isSkippable = true;

        /// <summary>主对话行。</summary>
        public DialogueLine Line
        {
            get => line;
            set => line = value;
        }

        /// <summary>备选行。</summary>
        public IReadOnlyList<DialogueLine> AlternativeLines => alternativeLines;

        /// <summary>后续 NPC 回复节点 ID。</summary>
        public IReadOnlyList<string> NpcReplyIds => npcReplyIds;

        /// <summary>玩家回复选项节点 ID。</summary>
        public IReadOnlyList<string> PlayerReplyIds => playerReplyIds;

        /// <summary>这条线说给谁听（说话人 ID），可空。</summary>
        public string DirectedAtSpeakerId
        {
            get => directedAtSpeakerId ?? string.Empty;
            set => directedAtSpeakerId = value;
        }

        /// <summary>是否可跳过。</summary>
        public bool IsSkippable
        {
            get => isSkippable;
            set => isSkippable = value;
        }

        public void AddAlternativeLine(DialogueLine altLine)
        {
            if (altLine != null)
            {
                (alternativeLines ??= new List<DialogueLine>()).Add(altLine);
            }
        }

        public void AddNpcReply(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                (npcReplyIds ??= new List<string>()).Add(id);
            }
        }

        public void AddPlayerReply(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                (playerReplyIds ??= new List<string>()).Add(id);
            }
        }

        /// <summary>
        /// 本节点里所有“选中条件满足”的行（主行 + 满足条件的备选行）。纯逻辑、可测。
        /// </summary>
        public List<DialogueLine> GetEligibleLines(NarrativeContext context)
        {
            var result = new List<DialogueLine>();

            if (line != null && line.AreConditionsMet(context))
            {
                result.Add(line);
            }

            if (alternativeLines != null)
            {
                foreach (var alt in alternativeLines)
                {
                    if (alt != null && alt.AreConditionsMet(context))
                    {
                        result.Add(alt);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 在满足条件的候选行里随机选一条（无候选则回退到主行）。对应 UE <c>GetRandomLine</c>。
        /// </summary>
        public DialogueLine GetRandomLine(NarrativeContext context)
        {
            var eligible = GetEligibleLines(context);
            if (eligible.Count == 0)
            {
                return line;
            }

            return eligible.Count == 1 ? eligible[0] : eligible[UnityEngine.Random.Range(0, eligible.Count)];
        }

        /// <summary>
        /// 是否是纯路由节点（不含任何对话文本，只用于连线）。对应 UE <c>IsRoutingNode</c>。
        /// </summary>
        public bool IsRoutingNode()
        {
            if (line != null && !string.IsNullOrEmpty(line.Text))
            {
                return false;
            }

            if (alternativeLines != null)
            {
                foreach (var alt in alternativeLines)
                {
                    if (alt != null && !string.IsNullOrEmpty(alt.Text))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
