// EditMode 测试：NarrativeComponent 宿主 + 内置条件/事件的集成。
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class CoreHostEditTests
    {
        private GameObject _go;
        private NarrativeComponent _host;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("NarrativeHost");
            _host = _go.AddComponent<NarrativeComponent>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        // data task 定义的 taskName 是私有 [SerializeField]，测试里用反射构造一个。
        private static DataTaskDefinition MakeTask(string taskName)
        {
            var t = ScriptableObject.CreateInstance<DataTaskDefinition>();
            typeof(DataTaskDefinition)
                .GetField("taskName", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(t, taskName);
            return t;
        }

        [Test]
        public void CompleteDataTask_StringOverload_RecordsAndBroadcasts()
        {
            string fired = null;
            _host.DataTaskCompleted += s => fired = s;

            Assert.IsTrue(_host.CompleteDataTask("KillNPC", "King"));

            Assert.AreEqual("killnpc_king", fired, "事件应携带规范化后的原始任务串");
            Assert.IsTrue(_host.MasterTasks.HasCompleted("killnpc_king"));
        }

        [Test]
        public void CompleteDataTask_DefinitionOverload_MatchesStringForm()
        {
            var task = MakeTask("KillNPC");
            _host.CompleteDataTask(task, "King", 2);

            Assert.AreEqual(2, _host.MasterTasks.GetCount("killnpc_king"),
                "资产版与字符串版应产生相同的原始任务串");
        }

        [Test]
        public void CompleteDataTask_RejectsEmptyName()
        {
            Assert.IsFalse(_host.CompleteDataTask("", "x"));
            Assert.IsFalse(_host.CompleteDataTask((DataTaskDefinition)null, "x"));
            Assert.AreEqual(0, _host.MasterTasks.DistinctCount);
        }

        [Test]
        public void Builtins_EventCompletesTask_ThenConditionPasses()
        {
            var task = MakeTask("TalkTo");
            var condition = new HasCompletedTaskCondition(task, "Elder");
            var completeEvent = new CompleteDataTaskEvent(task, "Elder");
            var ctx = _host.MakeContext();

            Assert.IsFalse(condition.IsMet(ctx), "事件执行前条件应不满足");

            completeEvent.Execute(ctx);

            Assert.IsTrue(condition.IsMet(ctx), "事件执行后条件应满足");
            Assert.IsTrue(_host.MasterTasks.HasCompleted("talkto_elder"));
        }

        [Test]
        public void Builtins_ViaNode_StartPhaseDrivesTaskProgress()
        {
            var task = MakeTask("TalkTo");
            var node = new NarrativeNodeBase();
            node.AddEvent(new CompleteDataTaskEvent(task, "Elder") { Runtime = EEventRuntime.Start });
            var ctx = _host.MakeContext();

            node.ProcessEvents(ctx, EEventRuntime.End);
            Assert.AreEqual(0, _host.MasterTasks.GetCount("talkto_elder"), "Start 事件不应在 End 阶段触发");

            node.ProcessEvents(ctx, EEventRuntime.Start);
            Assert.AreEqual(1, _host.MasterTasks.GetCount("talkto_elder"), "节点 Start 阶段应驱动 data-task 完成");
        }

        [Test]
        public void HasCompletedTaskCondition_RespectsQuantityThreshold()
        {
            var task = MakeTask("Gather");
            _host.CompleteDataTask(task, "Herb", 2);
            var ctx = _host.MakeContext();

            Assert.IsTrue(new HasCompletedTaskCondition(task, "Herb", 2).IsMet(ctx));
            Assert.IsFalse(new HasCompletedTaskCondition(task, "Herb", 3).IsMet(ctx), "未达数量阈值应不满足");
        }
    }
}
