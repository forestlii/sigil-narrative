// EditMode 测试：任务相关内置 BeginQuestEvent（开任务）+ QuestStateCondition（查任务状态），
// 以及扩展后的 INarrativeHost 任务面。对应 Task2。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class QuestBuiltinsEditTests
    {
        private static NarrativeComponent MakeHost(out GameObject go)
        {
            go = new GameObject("host");
            return go.AddComponent<NarrativeComponent>();
        }

        // 只有一个 regular 起始状态 → Begin 后保持进行中。
        private static QuestAsset SimpleInProgressQuest(string id)
        {
            var start = new QuestState("start");
            return QuestAsset.Create(id, "start", new List<QuestState> { start });
        }

        // start --b1[ 完成 trigger/Go x1 ]--> done(Success)。
        private static QuestAsset CompletableQuest(string id, DataTaskDefinition trigger)
        {
            var start = new QuestState("start");
            var b1 = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            b1.AddTask(new CompleteDataTaskQuestTask(trigger, "Go", 1));
            start.AddBranch(b1);
            var done = new QuestState("done", EStateNodeType.Success);
            return QuestAsset.Create(id, "start", new List<QuestState> { start, done });
        }

        // ---------------- BeginQuestEvent ----------------

        [Test]
        public void BeginQuestEvent_StartsQuestOnHost()
        {
            var questB = SimpleInProgressQuest("qB");
            var host = MakeHost(out var go);
            try
            {
                Assert.IsFalse(host.IsQuestInProgress(questB), "前置：qB 尚未开始");

                new BeginQuestEvent(questB).Execute(host.MakeContext());

                Assert.IsTrue(host.IsQuestInProgress(questB), "BeginQuestEvent 执行后 qB 应进行中");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BeginQuestEvent_DefaultsToRefireOnLoadFalse()
        {
            Assert.IsFalse(new BeginQuestEvent().RefireOnLoad,
                "开任务是一次性副作用，默认不应在读档时重触发");
        }

        [Test]
        public void BeginQuestEvent_NullQuest_NoThrow()
        {
            var host = MakeHost(out var go);
            try
            {
                Assert.DoesNotThrow(() => new BeginQuestEvent().Execute(host.MakeContext()));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BeginQuestEvent_FiresFromStateEntry_ChainsIntoSecondQuest()
        {
            // questA 的起始状态进入事件 = 开始 questB；开始 questA 应连带把 questB 也开起来。
            var questB = SimpleInProgressQuest("qB");
            var startA = new QuestState("start");
            startA.AddEvent(new BeginQuestEvent(questB));
            var questA = QuestAsset.Create("qA", "start", new List<QuestState> { startA });

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(questA);
                Assert.IsTrue(host.IsQuestInProgress(questA), "qA 应进行中");
                Assert.IsTrue(host.IsQuestInProgress(questB), "qA 的进入事件应把 qB 也开起来");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- QuestStateCondition ----------------

        [Test]
        public void QuestStateCondition_ReflectsInProgressThenSucceeded()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var quest = CompletableQuest("q", trigger);
            var host = MakeHost(out var go);
            try
            {
                var context = host.MakeContext();
                var inProgress = new QuestStateCondition(quest, EQuestStateQuery.InProgress);
                var succeeded = new QuestStateCondition(quest, EQuestStateQuery.Succeeded);
                var finished = new QuestStateCondition(quest, EQuestStateQuery.Finished);
                var startedOrFinished = new QuestStateCondition(quest, EQuestStateQuery.StartedOrFinished);

                host.BeginQuest(quest);
                Assert.IsTrue(inProgress.IsMet(context), "开始后应进行中");
                Assert.IsFalse(succeeded.IsMet(context), "尚未成功");
                Assert.IsTrue(startedOrFinished.IsMet(context), "已参与过");
                Assert.IsFalse(finished.IsMet(context), "尚未结束");

                host.CompleteDataTask("Trigger", "Go"); // → done(Success)
                Assert.IsFalse(inProgress.IsMet(context), "结束后不再进行中");
                Assert.IsTrue(succeeded.IsMet(context), "应已成功");
                Assert.IsTrue(finished.IsMet(context), "应已结束");
                Assert.IsTrue(startedOrFinished.IsMet(context), "仍算已参与过");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestStateCondition_NeverStartedQuest_AllQueriesFalse()
        {
            var quest = SimpleInProgressQuest("q_never");
            var host = MakeHost(out var go);
            try
            {
                var context = host.MakeContext();
                foreach (EQuestStateQuery q in System.Enum.GetValues(typeof(EQuestStateQuery)))
                {
                    Assert.IsFalse(new QuestStateCondition(quest, q).IsMet(context),
                        $"从未开始的任务，查询 {q} 应为 false");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void QuestStateCondition_Negate_InvertsResult()
        {
            var quest = SimpleInProgressQuest("q_neg");
            var host = MakeHost(out var go);
            try
            {
                var context = host.MakeContext();
                var notInProgress = new QuestStateCondition(quest, EQuestStateQuery.InProgress) { Not = true };

                Assert.IsTrue(notInProgress.IsMet(context), "未开始：'非进行中' 为真");
                host.BeginQuest(quest);
                Assert.IsFalse(notInProgress.IsMet(context), "进行中：'非进行中' 为假");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
