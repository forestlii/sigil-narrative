// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// NPC 对话节点。对应 UE 的 UDialogueNode_NPC。多了一个说话人 ID。

using System;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// NPC 说的一条对话节点。对应 UE <c>UDialogueNode_NPC</c>。
    /// </summary>
    [Serializable]
    public sealed class DialogueNode_NPC : DialogueNode
    {
        [Tooltip("本节点说话人的 ID。对应 UE SpeakerID。")]
        [SerializeField] private string speakerId;

        public DialogueNode_NPC() { }

        public DialogueNode_NPC(string id, DialogueLine line = null, string speakerId = null)
        {
            Id = id;
            if (line != null)
            {
                Line = line;
            }
            this.speakerId = speakerId;
        }

        /// <summary>说话人 ID。对应 UE GetSpeakerID/SetSpeakerID。</summary>
        public string SpeakerId
        {
            get => speakerId ?? string.Empty;
            set => speakerId = value;
        }
    }
}
