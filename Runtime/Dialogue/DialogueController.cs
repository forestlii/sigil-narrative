// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对话推进核心（纯逻辑，不碰表现）。对应 UE UDialogue 里 chunk 推进那部分逻辑：
// GenerateDialogueChunk / GetReplyChain / GetPlayerReplies / SelectDialogueOption / Play / NPCFinishedTalking。
// 一个 chunk = 一串连续 NPC 台词(回复链) + 链尾节点的合法玩家选项。表现层通过 AdvanceLine/SelectOption 驱动。

using System.Collections.Generic;

namespace Likeon.Narrative
{
    /// <summary>对话当前所处阶段。</summary>
    public enum DialoguePhase
    {
        /// <summary>尚未开始。</summary>
        Idle,
        /// <summary>正在播放 NPC 回复链里的一行。</summary>
        NpcLine,
        /// <summary>NPC 说完，等待玩家从可选项里选择。</summary>
        AwaitingChoice,
        /// <summary>正在播放玩家选中的那一行。</summary>
        PlayerLine,
        /// <summary>对话已结束。</summary>
        Ended,
    }

    /// <summary>
    /// 驱动一段对话的推进。逻辑与表现分离：本类只决定“谁说什么、有哪些选项、下一步去哪”，
    /// 何时“这一行播完”由 <see cref="IDialoguePresenter"/> 实现方判断并调用 <see cref="AdvanceLine"/>。
    /// </summary>
    public sealed class DialogueController
    {
        private readonly DialogueGraph _graph;
        private readonly NarrativeContext _context;
        private readonly IDialoguePresenter _presenter;

        private List<DialogueNode_NPC> _npcChain = new List<DialogueNode_NPC>();
        private List<DialogueNode_Player> _responses = new List<DialogueNode_Player>();
        private int _npcIndex;
        private DialogueNode_Player _selectedOption;

        public DialogueController(DialogueGraph graph, NarrativeContext context, IDialoguePresenter presenter)
        {
            _graph = graph;
            _context = context;
            _presenter = presenter;
        }

        /// <summary>当前阶段。</summary>
        public DialoguePhase Phase { get; private set; } = DialoguePhase.Idle;

        /// <summary>是否正在进行对话。</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>当前正在播放的节点（NPC 或玩家节点），可空。</summary>
        public DialogueNode CurrentNode { get; private set; }

        /// <summary>当前正在播放的行，可空。</summary>
        public DialogueLine CurrentLine { get; private set; }

        /// <summary>当前可选的玩家回复（仅 <see cref="DialoguePhase.AwaitingChoice"/> 有意义）。</summary>
        public IReadOnlyList<DialogueNode_Player> AvailableResponses => _responses;

        /// <summary>
        /// 开始对话：从根 NPC 节点生成首个 chunk。若无有效内容则不开始、返回 false。
        /// 对应 UE BeginDialogue。
        /// </summary>
        public bool Begin()
        {
            if (Phase != DialoguePhase.Idle)
            {
                return false;
            }

            var root = _graph?.GetRoot();
            if (root == null || !GenerateChunk(root))
            {
                return false;
            }

            IsPlaying = true;
            _presenter.OnDialogueBegan(this);
            StartNpcChain();
            return true;
        }

        /// <summary>
        /// 表现层在“当前行播放完毕（时长到/被跳过）”时调用，推进到下一行 / 玩家选项 / 下一 chunk / 结束。
        /// </summary>
        public void AdvanceLine()
        {
            if (Phase == DialoguePhase.NpcLine)
            {
                FinishCurrent();
                _npcIndex++;
                if (_npcIndex < _npcChain.Count)
                {
                    PlayNode(_npcChain[_npcIndex]);
                }
                else
                {
                    PresentResponses();
                }
            }
            else if (Phase == DialoguePhase.PlayerLine)
            {
                FinishCurrent();
                ContinueAfterPlayerLine();
            }
            // Idle/AwaitingChoice/Ended 阶段忽略。
        }

        /// <summary>
        /// 玩家选择一个回复选项。必须处于等待选择阶段、且该选项在可选列表里。对应 UE SelectDialogueOption。
        /// </summary>
        public bool SelectOption(DialogueNode_Player option)
        {
            if (Phase != DialoguePhase.AwaitingChoice || option == null || !_responses.Contains(option))
            {
                return false;
            }

            SelectOptionInternal(option);
            return true;
        }

        // ---------------- 内部推进 ----------------

        private void StartNpcChain()
        {
            _npcIndex = 0;
            if (_npcChain.Count == 0)
            {
                PresentResponses();
                return;
            }

            Phase = DialoguePhase.NpcLine;
            PlayNode(_npcChain[0]);
        }

        private void PlayNode(DialogueNode node)
        {
            CurrentNode = node;
            CurrentLine = node.GetRandomLine(_context);
            node.ProcessEvents(_context, EEventRuntime.Start);
            _presenter.OnLineStarted(node, CurrentLine);
        }

        private void FinishCurrent()
        {
            if (CurrentNode == null)
            {
                return;
            }

            CurrentNode.ProcessEvents(_context, EEventRuntime.End);
            _presenter.OnLineFinished(CurrentNode, CurrentLine);
        }

        private void PresentResponses()
        {
            if (_responses.Count == 0)
            {
                End();
                return;
            }

            // 自动选择项（IsAutoSelect：显式标记或路由节点）直接选中，不等 UI。
            foreach (var response in _responses)
            {
                if (response != null && response.IsAutoSelect())
                {
                    SelectOptionInternal(response);
                    return;
                }
            }

            Phase = DialoguePhase.AwaitingChoice;
            _presenter.OnResponsesAvailable(_responses);
        }

        private void SelectOptionInternal(DialogueNode_Player option)
        {
            _selectedOption = option;
            Phase = DialoguePhase.PlayerLine;
            PlayNode(option);
        }

        private void ContinueAfterPlayerLine()
        {
            var nextStart = FirstValidNpc(_selectedOption != null ? _selectedOption.NpcReplyIds : null, null);
            if (nextStart != null && GenerateChunk(nextStart))
            {
                StartNpcChain();
            }
            else
            {
                End();
            }
        }

        private void End()
        {
            Phase = DialoguePhase.Ended;
            IsPlaying = false;
            CurrentNode = null;
            CurrentLine = null;
            _presenter.OnDialogueEnded(this);
        }

        // ---------------- chunk 生成 ----------------

        private bool GenerateChunk(DialogueNode_NPC start)
        {
            _npcChain = BuildReplyChain(start);
            _responses = _npcChain.Count > 0
                ? GetValidPlayerReplies(_npcChain[_npcChain.Count - 1])
                : new List<DialogueNode_Player>();
            return HasValidChunk();
        }

        /// <summary>从起始 NPC 节点顺着“每步第一个条件满足的 NPC 回复”串出回复链。对应 UE GetReplyChain。</summary>
        private List<DialogueNode_NPC> BuildReplyChain(DialogueNode_NPC start)
        {
            var chain = new List<DialogueNode_NPC>();
            var visited = new HashSet<DialogueNode_NPC>();
            var current = start;

            while (current != null && visited.Add(current))
            {
                chain.Add(current);
                // 排除起始节点自身作为下一跳（对齐 UE 的 Reply != this）；visited 兜底一般环。
                current = FirstValidNpc(current.NpcReplyIds, start.Id);
            }

            return chain;
        }

        private List<DialogueNode_Player> GetValidPlayerReplies(DialogueNode lastNpc)
        {
            var result = new List<DialogueNode_Player>();
            if (lastNpc == null)
            {
                return result;
            }

            foreach (var id in lastNpc.PlayerReplyIds)
            {
                var player = _graph.GetPlayerNode(id);
                if (player != null && player.AreConditionsMet(_context))
                {
                    result.Add(player);
                }
            }

            return result;
        }

        private DialogueNode_NPC FirstValidNpc(IReadOnlyList<string> ids, string excludeId)
        {
            if (ids == null)
            {
                return null;
            }

            foreach (var id in ids)
            {
                if (!string.IsNullOrEmpty(excludeId) && id == excludeId)
                {
                    continue;
                }

                var npc = _graph.GetNpcNode(id);
                if (npc != null && npc.AreConditionsMet(_context))
                {
                    return npc;
                }
            }

            return null;
        }

        private bool HasValidChunk()
        {
            if (_responses.Count > 0)
            {
                return true;
            }

            foreach (var npc in _npcChain)
            {
                if (npc != null && !npc.IsRoutingNode())
                {
                    return true;
                }
            }

            return false;
        }
    }
}
