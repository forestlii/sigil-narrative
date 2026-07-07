// EditMode 测试：DialogueController 的 chunk 推进（用记录型 presenter 断言事件序列）。
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    // 记录型表现层：把每个回调记成一条字符串，便于断言完整序列。
    internal sealed class RecordingPresenter : IDialoguePresenter
    {
        public readonly List<string> Log = new List<string>();

        public void OnDialogueBegan(DialogueController controller) => Log.Add("began");
        public void OnLineStarted(DialogueNode node, DialogueLine line) => Log.Add("start:" + node.Id);
        public void OnLineFinished(DialogueNode node, DialogueLine line) => Log.Add("finish:" + node.Id);
        public void OnResponsesAvailable(IReadOnlyList<DialogueNode_Player> options)
            => Log.Add("responses:" + string.Join(",", options.Select(o => o.Id)));
        public void OnDialogueEnded(DialogueController controller) => Log.Add("ended");
    }

    public class DialogueControllerEditTests
    {
        private static NarrativeContext Ctx() => new NarrativeContext(null);

        // n1(NPC) --replies--> [p1, p2]; p1 --> n2(NPC); p2 结束; n2 结束
        private static DialogueGraph BuildBranchingGraph(
            out DialogueNode_NPC n1, out DialogueNode_Player p1, out DialogueNode_Player p2, out DialogueNode_NPC n2)
        {
            var graph = new DialogueGraph { RootId = "n1" };
            n1 = new DialogueNode_NPC("n1", new DialogueLine("Hello traveler."));
            n1.AddPlayerReply("p1");
            n1.AddPlayerReply("p2");

            p1 = new DialogueNode_Player("p1", new DialogueLine("Who are you?"));
            p1.AddNpcReply("n2");
            p2 = new DialogueNode_Player("p2", new DialogueLine("Goodbye."));
            n2 = new DialogueNode_NPC("n2", new DialogueLine("I am the elder."));

            graph.AddNode(n1);
            graph.AddNode(p1);
            graph.AddNode(p2);
            graph.AddNode(n2);
            return graph;
        }

        [Test]
        public void FullWalk_SelectingOption_EmitsExpectedSequence()
        {
            var graph = BuildBranchingGraph(out _, out var p1, out _, out _);
            var presenter = new RecordingPresenter();
            var c = new DialogueController(graph, Ctx(), presenter);

            Assert.IsTrue(c.Begin());
            c.AdvanceLine();                       // n1 说完 → 出选项
            Assert.AreEqual(DialoguePhase.AwaitingChoice, c.Phase);
            Assert.AreEqual(2, c.AvailableResponses.Count);

            Assert.IsTrue(c.SelectOption(p1));     // 选“Who are you?”
            c.AdvanceLine();                       // p1 说完 → 生成 n2 chunk → n2 开始
            c.AdvanceLine();                       // n2 说完 → 无选项 → 结束

            Assert.AreEqual(DialoguePhase.Ended, c.Phase);
            Assert.IsFalse(c.IsPlaying);
            CollectionAssert.AreEqual(
                new[] { "began", "start:n1", "finish:n1", "responses:p1,p2", "start:p1", "finish:p1", "start:n2", "finish:n2", "ended" },
                presenter.Log);
        }

        [Test]
        public void PlayerOptions_FilteredByConditions()
        {
            var graph = BuildBranchingGraph(out _, out var p1, out var p2, out _);
            p1.AddCondition(new FakeCondition(false)); // p1 被条件挡掉

            var presenter = new RecordingPresenter();
            var c = new DialogueController(graph, Ctx(), presenter);

            c.Begin();
            c.AdvanceLine();

            Assert.AreEqual(1, c.AvailableResponses.Count);
            Assert.AreSame(p2, c.AvailableResponses[0]);
            Assert.AreEqual("responses:p2", presenter.Log.Last());
        }

        [Test]
        public void AutoSelectResponse_SkipsWaitingForChoice()
        {
            var graph = BuildBranchingGraph(out _, out var p1, out _, out _);
            p1.AutoSelect = true; // p1 自动选中（在列表最前）

            var presenter = new RecordingPresenter();
            var c = new DialogueController(graph, Ctx(), presenter);

            c.Begin();
            c.AdvanceLine(); // n1 说完 → 自动选 p1（不等 UI）→ 直接播 p1

            Assert.AreEqual(DialoguePhase.PlayerLine, c.Phase);
            Assert.AreSame(p1, c.CurrentNode);
            Assert.IsFalse(presenter.Log.Any(l => l.StartsWith("responses:")), "自动选择不应发出等待选择回调");
        }

        [Test]
        public void NpcReplyChain_PlaysMultipleNpcLinesBeforeResponses()
        {
            var graph = new DialogueGraph { RootId = "n1" };
            var n1 = new DialogueNode_NPC("n1", new DialogueLine("Line one."));
            n1.AddNpcReply("n1b"); // NPC 回复链的下一节点
            var n1b = new DialogueNode_NPC("n1b", new DialogueLine("Line two."));
            n1b.AddPlayerReply("p1");
            var p1 = new DialogueNode_Player("p1", new DialogueLine("Ok."));
            graph.AddNode(n1);
            graph.AddNode(n1b);
            graph.AddNode(p1);

            var presenter = new RecordingPresenter();
            var c = new DialogueController(graph, Ctx(), presenter);

            c.Begin();       // 播 n1
            c.AdvanceLine(); // n1→n1b（同一 chunk 内的回复链）
            c.AdvanceLine(); // n1b 说完 → 出选项

            CollectionAssert.AreEqual(
                new[] { "began", "start:n1", "finish:n1", "start:n1b", "finish:n1b", "responses:p1" },
                presenter.Log);
        }

        [Test]
        public void Begin_ReturnsFalse_WhenChunkHasNoContent()
        {
            // 根是空文本、无事件、无选项的路由节点 → 无有效内容
            var graph = new DialogueGraph { RootId = "r" };
            graph.AddNode(new DialogueNode_NPC("r")); // 空行

            var presenter = new RecordingPresenter();
            var c = new DialogueController(graph, Ctx(), presenter);

            Assert.IsFalse(c.Begin(), "无有效内容的对话不应开始");
            Assert.AreEqual(DialoguePhase.Idle, c.Phase);
            Assert.IsEmpty(presenter.Log);
        }

        [Test]
        public void SelectOption_RejectedOutsideAwaitingChoice()
        {
            var graph = BuildBranchingGraph(out _, out var p1, out _, out _);
            var c = new DialogueController(graph, Ctx(), new RecordingPresenter());

            // 还没到选择阶段
            Assert.IsFalse(c.SelectOption(p1));
            c.Begin(); // 正在播 n1（NpcLine 阶段）
            Assert.IsFalse(c.SelectOption(p1), "非等待选择阶段应拒绝选择");
        }
    }
}
