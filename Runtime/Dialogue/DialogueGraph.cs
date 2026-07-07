// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对话图：扁平节点表 + 按 string ID 查找。对应 UE 编译后 UDialogue 里的扁平 NPCReplies/PlayerReplies 集合。
// 这是“跳过图编辑器、直接用扁平模板”设计的落点——节点间用 ID 互引，图负责按 ID 解析。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一个对话的扁平节点图。持有根 NPC 节点 ID 与全部节点（NPC/玩家混在一张表），按 ID 查询。
    /// 对应 UE <c>UDialogue</c> 里的 RootDialogue + NPCReplies + PlayerReplies + 按 ID 取节点的一族辅助函数。
    /// </summary>
    [Serializable]
    public sealed class DialogueGraph
    {
        [Tooltip("根节点（NPC 先开口）的 ID。对应 UE RootDialogue。")]
        [SerializeField] private string rootId;

        [Tooltip("图里所有节点（NPC 与玩家节点混合，多态序列化）。")]
        [SerializeReference] private List<DialogueNode> nodes = new List<DialogueNode>();

        [NonSerialized] private Dictionary<string, DialogueNode> _lookup;

        /// <summary>根节点 ID（NPC 节点）。</summary>
        public string RootId
        {
            get => rootId ?? string.Empty;
            set => rootId = value;
        }

        /// <summary>全部节点。</summary>
        public IReadOnlyList<DialogueNode> Nodes => nodes;

        /// <summary>加入一个节点（并使查找缓存失效）。</summary>
        public void AddNode(DialogueNode node)
        {
            if (node == null)
            {
                return;
            }

            (nodes ??= new List<DialogueNode>()).Add(node);
            _lookup = null;
        }

        /// <summary>按 ID 取节点，找不到返回 null。对应 UE GetNPCReplyByID/GetPlayerReplyByID。</summary>
        public DialogueNode GetNode(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            EnsureLookup();
            return _lookup.TryGetValue(id, out var node) ? node : null;
        }

        /// <summary>按 ID 取 NPC 节点（类型不符返回 null）。</summary>
        public DialogueNode_NPC GetNpcNode(string id) => GetNode(id) as DialogueNode_NPC;

        /// <summary>按 ID 取玩家节点（类型不符返回 null）。</summary>
        public DialogueNode_Player GetPlayerNode(string id) => GetNode(id) as DialogueNode_Player;

        /// <summary>根 NPC 节点。</summary>
        public DialogueNode_NPC GetRoot() => GetNpcNode(rootId);

        /// <summary>把一组 ID 解析成节点（跳过找不到的）。</summary>
        public List<DialogueNode> ResolveNodes(IEnumerable<string> ids)
        {
            var result = new List<DialogueNode>();
            if (ids == null)
            {
                return result;
            }

            foreach (var id in ids)
            {
                var node = GetNode(id);
                if (node != null)
                {
                    result.Add(node);
                }
            }

            return result;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<string, DialogueNode>();
            if (nodes == null)
            {
                return;
            }

            foreach (var node in nodes)
            {
                if (node != null && !string.IsNullOrEmpty(node.Id) && !_lookup.ContainsKey(node.Id))
                {
                    _lookup[node.Id] = node;
                }
            }
        }
    }
}
