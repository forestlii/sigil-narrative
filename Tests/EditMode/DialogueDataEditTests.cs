// EditMode 测试：对话数据模型（行时长/条件过滤、节点路由/自动选、图 by-ID 查找）。
using NUnit.Framework;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class DialogueDataEditTests
    {
        private static NarrativeContext Ctx() => new NarrativeContext(null);

        // ---------- DialogueLine ----------

        [Test]
        public void GetReadingTime_EmptyIsZero_ElseClampedToMin()
        {
            Assert.AreEqual(0f, new DialogueLine("").GetReadingTime(10f, 1f), 1e-4f, "空文本应为 0");

            // 20 字 / 每秒 10 字 = 2 秒（>最小 1 秒）
            Assert.AreEqual(2f, new DialogueLine("12345678901234567890").GetReadingTime(10f, 1f), 1e-4f);

            // 5 字 / 10 = 0.5 秒 → 被最小显示时长 1 秒抬高
            Assert.AreEqual(1f, new DialogueLine("12345").GetReadingTime(10f, 1f), 1e-4f, "应不低于最小显示时长");
        }

        [Test]
        public void ResolveEffectiveDuration_MirrorsDefaultFallback()
        {
            // 非 Default 原样返回
            Assert.AreEqual(ELineDuration.Never,
                new DialogueLine("hi", ELineDuration.Never).ResolveEffectiveDuration(true, true));

            var line = new DialogueLine("hi"); // Default + 有文本
            Assert.AreEqual(ELineDuration.WhenAudioEnds, line.ResolveEffectiveDuration(hasAudio: true, hasSequence: false));
            Assert.AreEqual(ELineDuration.AfterReadingTime, line.ResolveEffectiveDuration(hasAudio: false, hasSequence: false));

            var empty = new DialogueLine(""); // Default + 无文本 + 有序列
            Assert.AreEqual(ELineDuration.WhenSequenceEnds, empty.ResolveEffectiveDuration(hasAudio: false, hasSequence: true));
        }

        [Test]
        public void Line_AreConditionsMet_GatesSelection()
        {
            var line = new DialogueLine("secret");
            Assert.IsTrue(line.AreConditionsMet(Ctx()), "无条件视为满足");

            line.AddCondition(new FakeCondition(false));
            Assert.IsFalse(line.AreConditionsMet(Ctx()));
        }

        // ---------- DialogueNode ----------

        [Test]
        public void Node_GetEligibleLines_FiltersByConditions()
        {
            var node = new DialogueNode_NPC("n1");

            var main = new DialogueLine("main");
            main.AddCondition(new FakeCondition(false)); // 主行被条件挡掉
            node.Line = main;

            var alt = new DialogueLine("alt"); // 备选行无条件 → 入选
            node.AddAlternativeLine(alt);

            var eligible = node.GetEligibleLines(Ctx());
            Assert.AreEqual(1, eligible.Count);
            Assert.AreEqual("alt", eligible[0].Text);

            // 只有一条候选 → GetRandomLine 必返回它
            Assert.AreEqual("alt", node.GetRandomLine(Ctx()).Text);
        }

        [Test]
        public void Node_GetRandomLine_FallsBackToMainWhenNoneEligible()
        {
            var node = new DialogueNode_NPC("n1");
            var main = new DialogueLine("main");
            main.AddCondition(new FakeCondition(false));
            node.Line = main;

            // 无任何候选满足条件 → 回退到主行
            Assert.AreEqual("main", node.GetRandomLine(Ctx()).Text);
        }

        [Test]
        public void Node_IsRoutingNode_TrueOnlyWhenNoText()
        {
            Assert.IsTrue(new DialogueNode_NPC("r").IsRoutingNode(), "空行应视为路由节点");
            Assert.IsFalse(new DialogueNode_NPC("n", new DialogueLine("hi")).IsRoutingNode());
        }

        [Test]
        public void PlayerNode_OptionText_FallsBackToLineText()
        {
            var withOption = new DialogueNode_Player("p1", new DialogueLine("full line"), "Short");
            Assert.AreEqual("Short", withOption.GetOptionText());

            var noOption = new DialogueNode_Player("p2", new DialogueLine("full line"));
            Assert.AreEqual("full line", noOption.GetOptionText(), "选项文本为空应回退到主行文本");
        }

        [Test]
        public void PlayerNode_IsAutoSelect_WhenFlaggedOrRouting()
        {
            var flagged = new DialogueNode_Player("p1", new DialogueLine("hi")) { AutoSelect = true };
            Assert.IsTrue(flagged.IsAutoSelect());

            var routing = new DialogueNode_Player("p2"); // 空文本 → 路由
            Assert.IsTrue(routing.IsAutoSelect(), "路由节点应自动选择");

            var normal = new DialogueNode_Player("p3", new DialogueLine("pick me"));
            Assert.IsFalse(normal.IsAutoSelect());
        }

        // ---------- DialogueGraph ----------

        [Test]
        public void Graph_ByIdLookup_ResolvesTypesAndMisses()
        {
            var graph = new DialogueGraph { RootId = "n1" };
            graph.AddNode(new DialogueNode_NPC("n1", new DialogueLine("hello")));
            graph.AddNode(new DialogueNode_Player("p1", new DialogueLine("hi back")));
            graph.AddNode(new DialogueNode_NPC("n2", new DialogueLine("bye")));

            Assert.AreEqual("n1", graph.GetRoot().Id);
            Assert.IsNotNull(graph.GetNpcNode("n1"));
            Assert.IsNotNull(graph.GetPlayerNode("p1"));
            Assert.IsNull(graph.GetNode("missing"), "找不到应返回 null");
            Assert.IsNull(graph.GetNpcNode("p1"), "类型不符应返回 null");

            var resolved = graph.ResolveNodes(new[] { "n1", "p1", "nope" });
            Assert.AreEqual(2, resolved.Count, "解析应跳过找不到的 ID");
        }

        [Test]
        public void Graph_AddNode_InvalidatesLookupCache()
        {
            var graph = new DialogueGraph();
            Assert.IsNull(graph.GetNode("late"));
            graph.AddNode(new DialogueNode_NPC("late", new DialogueLine("added later")));
            Assert.IsNotNull(graph.GetNode("late"), "新增节点后应能查到（缓存失效重建）");
        }
    }
}
