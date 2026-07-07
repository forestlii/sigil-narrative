// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 玩家对话节点（一个可选回复选项）。对应 UE 的 UDialogueNode_Player。

using System;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 玩家的一个回复选项。对应 UE <c>UDialogueNode_Player</c>：
    /// 有简短的选项文本、提示文本、以及“是否自动选择”。
    /// </summary>
    [Serializable]
    public sealed class DialogueNode_Player : DialogueNode
    {
        [Tooltip("选项列表里显示的简短文本；留空则用主行文本。对应 UE OptionText。")]
        [SerializeField] private string optionText;

        [Tooltip("选项文本后的提示（如“(撒谎)”“(开始任务)”）。对应 UE HintText。")]
        [SerializeField] private string hintText;

        [Tooltip("为真则该选项自动选中，玩家无需手动点。对应 UE bAutoSelect。")]
        [SerializeField] private bool autoSelect = false;

        public DialogueNode_Player() { }

        public DialogueNode_Player(string id, DialogueLine line = null, string optionText = null)
        {
            Id = id;
            if (line != null)
            {
                Line = line;
            }
            this.optionText = optionText;
        }

        /// <summary>提示文本。</summary>
        public string HintText
        {
            get => hintText ?? string.Empty;
            set => hintText = value;
        }

        /// <summary>是否配置为自动选择。</summary>
        public bool AutoSelect
        {
            get => autoSelect;
            set => autoSelect = value;
        }

        /// <summary>
        /// 选项显示文本：优先 <see cref="optionText"/>，为空则回退到主行文本。对应 UE GetOptionText。
        /// </summary>
        public string GetOptionText()
        {
            return string.IsNullOrEmpty(optionText) ? Line?.Text ?? string.Empty : optionText;
        }

        /// <summary>
        /// 是否应自动选择：显式设了 <see cref="AutoSelect"/>，或它是纯路由节点。对应 UE <c>IsAutoSelect</c>。
        /// </summary>
        public bool IsAutoSelect()
        {
            return autoSelect || IsRoutingNode();
        }
    }
}
