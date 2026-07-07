// EditMode 测试：Quest 状态机（任务完成取分支、成功/失败、optional、data-task 驱动、进度）。
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    // 手动推进的测试任务：暴露 Advance/Complete 以便测试驱动进度。
    internal sealed class ManualQuestTask : QuestTask
    {
        public ManualQuestTask(int required = 1, bool optional = false)
        {
            SetRequiredQuantityForConstruction(required);
            SetOptionalForConstruction(optional);
        }

        public void Advance(int n = 1) => AddProgress(n);
    }

    public class QuestEditTests
    {
        // 记录 quest 事件。
        private sealed class QuestLog
        {
            public readonly List<string> Events = new List<string>();

            public void Attach(Quest q)
            {
                q.Started += _ => Events.Add("started");
                q.NewState += (_, s) => Events.Add("state:" + s.Id);
                q.TaskProgressChanged += (_, b, t, o, n) => Events.Add($"progress:{o}->{n}");
                q.TaskCompleted += (_, b, t) => Events.Add("taskDone");
                q.BranchCompleted += (_, b) => Events.Add("branch:" + b.Id);
                q.Succeeded += _ => Events.Add("succeeded");
                q.Failed += _ => Events.Add("failed");
            }
        }

        // start --branch b1(tasks)--> end(endType)
        private static Quest BuildLinearQuest(NarrativeContext ctx, EStateNodeType endType, out QuestBranch branch, params QuestTask[] tasks)
        {
            var start = new QuestState("start");
            branch = new QuestBranch { Id = "b1", DestinationStateId = "end" };
            foreach (var t in tasks)
            {
                branch.AddTask(t);
            }
            start.AddBranch(branch);
            var end = new QuestState("end", endType);
            return new Quest("start", new[] { start, end }, ctx);
        }

        private static DataTaskDefinition MakeTask(string taskName)
        {
            var t = ScriptableObject.CreateInstance<DataTaskDefinition>();
            typeof(DataTaskDefinition)
                .GetField("taskName", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(t, taskName);
            return t;
        }

        [Test]
        public void BasicFlow_CompletingTask_TakesBranchToSuccess()
        {
            var ctx = new NarrativeContext(null);
            var task = new ManualQuestTask(1);
            var q = BuildLinearQuest(ctx, EStateNodeType.Success, out _, task);
            var log = new QuestLog();
            log.Attach(q);

            Assert.IsTrue(q.Begin());
            Assert.AreEqual("start", q.CurrentState.Id);
            Assert.IsTrue(q.IsInProgress);

            task.Complete();

            Assert.IsTrue(q.IsSucceeded);
            Assert.AreEqual("end", q.CurrentState.Id);
            CollectionAssert.Contains(log.Events, "started");
            CollectionAssert.Contains(log.Events, "state:start");
            CollectionAssert.Contains(log.Events, "branch:b1");
            CollectionAssert.Contains(log.Events, "state:end");
            CollectionAssert.Contains(log.Events, "succeeded");
            CollectionAssert.Contains(log.Events, "taskDone");
        }

        [Test]
        public void OptionalTask_DoesNotBlockBranchCompletion()
        {
            var ctx = new NarrativeContext(null);
            var required = new ManualQuestTask(1);
            var optional = new ManualQuestTask(1, optional: true);
            var q = BuildLinearQuest(ctx, EStateNodeType.Success, out var branch, required, optional);

            q.Begin();
            Assert.IsFalse(branch.AreTasksComplete(), "必做任务未完成 → 分支未完成");

            required.Complete(); // 可选任务从未完成，但恒视为完成
            Assert.IsTrue(q.IsSucceeded, "完成必做任务即应取分支（可选任务不阻塞）");
        }

        [Test]
        public void DataTaskCompletion_DrivesQuestProgress()
        {
            var go = new GameObject("host");
            var host = go.AddComponent<NarrativeComponent>();
            try
            {
                var dt = MakeTask("TalkTo");
                var qtask = new CompleteDataTaskQuestTask(dt, "Elder", 2);
                var q = BuildLinearQuest(host.MakeContext(), EStateNodeType.Success, out _, qtask);
                q.Begin();

                host.CompleteDataTask("TalkTo", "Elder");
                Assert.AreEqual(1, qtask.CurrentProgress);
                Assert.IsTrue(q.IsInProgress, "1/2 时任务仍进行中");

                host.CompleteDataTask("TalkTo", "Elder");
                Assert.AreEqual(2, qtask.CurrentProgress);
                Assert.IsTrue(q.IsSucceeded, "2/2 时应完成并成功");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FailureState_SetsQuestFailed()
        {
            var ctx = new NarrativeContext(null);
            var task = new ManualQuestTask(1);
            var q = BuildLinearQuest(ctx, EStateNodeType.Failure, out _, task);

            q.Begin();
            task.Complete();

            Assert.IsTrue(q.IsFailed);
            Assert.IsFalse(q.IsSucceeded);
        }

        [Test]
        public void TaskProgressText_ShowsFraction()
        {
            var ctx = new NarrativeContext(null);
            var task = new ManualQuestTask(3);
            var q = BuildLinearQuest(ctx, EStateNodeType.Success, out _, task);

            q.Begin();
            task.Advance(1);

            Assert.AreEqual("1/3", task.GetProgressText());
            Assert.IsTrue(q.IsInProgress);
        }

        [Test]
        public void Begin_Guards_AgainstReBeginAndMissingStart()
        {
            var ctx = new NarrativeContext(null);
            var q = BuildLinearQuest(ctx, EStateNodeType.Success, out _, new ManualQuestTask());
            Assert.IsTrue(q.Begin());
            Assert.IsFalse(q.Begin(), "已开始的任务不应再次 Begin");

            var noStart = new Quest("missing", new[] { new QuestState("x") }, ctx);
            Assert.IsFalse(noStart.Begin(), "找不到起始状态应返回 false");
        }
    }
}
