// EditMode 测试：条件/事件/节点框架（用假条件与假事件驱动，不依赖 Unity 对象）。
using NUnit.Framework;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    // 假条件：返回固定结果并计数被调用次数。
    internal sealed class FakeCondition : NarrativeCondition
    {
        public bool Result;
        public int Calls;

        public FakeCondition(bool result) { Result = result; }

        public override bool CheckCondition(NarrativeContext context)
        {
            Calls++;
            return Result;
        }
    }

    // 假事件：记录被执行的次数。
    internal sealed class FakeEvent : NarrativeEvent
    {
        public int Executed;

        public override void Execute(NarrativeContext context)
        {
            Executed++;
        }
    }

    public class CoreFrameworkEditTests
    {
        private static NarrativeContext Ctx() => new NarrativeContext(null);

        [Test]
        public void Condition_Not_FlipsResult()
        {
            var t = new FakeCondition(true);
            Assert.IsTrue(t.IsMet(Ctx()));
            t.Not = true;
            Assert.IsFalse(t.IsMet(Ctx()), "Not 应翻转 true→false");

            var f = new FakeCondition(false) { Not = true };
            Assert.IsTrue(f.IsMet(Ctx()), "Not 应翻转 false→true");
        }

        [Test]
        public void Node_AreConditionsMet_AllMustPass()
        {
            var node = new NarrativeNodeBase();
            Assert.IsTrue(node.AreConditionsMet(Ctx()), "无条件视为满足");

            node.AddCondition(new FakeCondition(true));
            node.AddCondition(new FakeCondition(true));
            Assert.IsTrue(node.AreConditionsMet(Ctx()), "全 true 应满足");

            node.AddCondition(new FakeCondition(false));
            Assert.IsFalse(node.AreConditionsMet(Ctx()), "有一个 false 即不满足");
        }

        [Test]
        public void Event_RunsAt_RespectsRuntimePhase()
        {
            Assert.IsTrue(new FakeEvent { Runtime = EEventRuntime.Start }.RunsAt(EEventRuntime.Start));
            Assert.IsFalse(new FakeEvent { Runtime = EEventRuntime.Start }.RunsAt(EEventRuntime.End));
            Assert.IsTrue(new FakeEvent { Runtime = EEventRuntime.End }.RunsAt(EEventRuntime.End));
            Assert.IsTrue(new FakeEvent { Runtime = EEventRuntime.Both }.RunsAt(EEventRuntime.Start), "Both 在 Start 触发");
            Assert.IsTrue(new FakeEvent { Runtime = EEventRuntime.Both }.RunsAt(EEventRuntime.End), "Both 在 End 触发");
        }

        [Test]
        public void Node_ProcessEvents_FiltersByPhase()
        {
            var node = new NarrativeNodeBase();
            var startEvt = new FakeEvent { Runtime = EEventRuntime.Start };
            var endEvt = new FakeEvent { Runtime = EEventRuntime.End };
            var bothEvt = new FakeEvent { Runtime = EEventRuntime.Both };
            node.AddEvent(startEvt);
            node.AddEvent(endEvt);
            node.AddEvent(bothEvt);

            node.ProcessEvents(Ctx(), EEventRuntime.Start);
            Assert.AreEqual(1, startEvt.Executed);
            Assert.AreEqual(0, endEvt.Executed, "End 事件不应在 Start 阶段触发");
            Assert.AreEqual(1, bothEvt.Executed, "Both 事件应在 Start 阶段触发");

            node.ProcessEvents(Ctx(), EEventRuntime.End);
            Assert.AreEqual(1, startEvt.Executed, "Start 事件不应在 End 阶段再触发");
            Assert.AreEqual(1, endEvt.Executed);
            Assert.AreEqual(2, bothEvt.Executed, "Both 事件在 End 阶段再触发一次");
        }

        [Test]
        public void Node_ProcessEvents_GatedByEventConditions()
        {
            var node = new NarrativeNodeBase();

            var blocked = new FakeEvent { Runtime = EEventRuntime.Start };
            blocked.AddCondition(new FakeCondition(false));
            node.AddEvent(blocked);

            var allowed = new FakeEvent { Runtime = EEventRuntime.Start };
            allowed.AddCondition(new FakeCondition(true));
            node.AddEvent(allowed);

            node.ProcessEvents(Ctx(), EEventRuntime.Start);

            Assert.AreEqual(0, blocked.Executed, "前置条件不满足的事件不应执行");
            Assert.AreEqual(1, allowed.Executed, "前置条件满足的事件应执行");
        }
    }
}
