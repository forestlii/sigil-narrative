// EditMode 测试：任务分支事件（激活发 Start、被取用/离开发 End）+ 幂等（不双触发）+ RefireOnLoad 读档过滤。
// 对应 Task1：QuestBranch 继承 NarrativeNodeBase，与 QuestState 一样带条件/事件。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class QuestBranchEventEditTests
    {
        private static NarrativeComponent MakeHost(out GameObject go)
        {
            go = new GameObject("host");
            return go.AddComponent<NarrativeComponent>();
        }

        // start --b1[ 完成 Trigger/Go x1 ]--> done(Success)。返回资产并把 b1 暴露出来挂事件。
        private static QuestAsset BuildQuest(
            DataTaskDefinition trigger,
            out QuestBranch b1,
            out QuestState start,
            out QuestState done)
        {
            start = new QuestState("start");
            b1 = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            b1.AddTask(new CompleteDataTaskQuestTask(trigger, "Go", 1));
            start.AddBranch(b1);
            done = new QuestState("done", EStateNodeType.Success);
            return QuestAsset.Create("q_branch_events", "start", new List<QuestState> { start, done });
        }

        [Test]
        public void BranchEntryEvent_FiresWhenOwningStateEntered()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var entered = DataTaskDefinition.Create("Entered");

            var asset = BuildQuest(trigger, out var b1, out _, out _);
            b1.AddEvent(new CompleteDataTaskEvent(entered, "Branch")); // 默认 Start：分支激活时触发

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset); // 进入 start → 激活 b1 → 触发 b1 的 Start 事件
                Assert.AreEqual(1, host.MasterTasks.GetCount("entered_branch"),
                    "进入分支所属状态、分支被激活时应触发分支的 Start 事件");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BranchExitEvent_FiresExactlyOnce_WhenBranchTaken()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var exit = DataTaskDefinition.Create("Exit");

            var asset = BuildQuest(trigger, out var b1, out _, out _);
            b1.AddEvent(new CompleteDataTaskEvent(exit, "Branch") { Runtime = EEventRuntime.End }); // 被取用时触发

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);
                Assert.AreEqual(0, host.MasterTasks.GetCount("exit_branch"), "还没取分支");

                host.CompleteDataTask("Trigger", "Go"); // 达标 → 取 b1（Deactivate）→ End 事件
                // 关键：取分支时 b1 会被 TakeBranch 与随后旧状态 Deactivate 各触碰一次，幂等保证只触发一次。
                Assert.AreEqual(1, host.MasterTasks.GetCount("exit_branch"),
                    "分支被取用时应恰好触发一次 End 事件（幂等，不双触发）");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SiblingBranchExitEvent_FiresOnce_WhenAnotherBranchTaken()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var other = DataTaskDefinition.Create("Other");
            var siblingExit = DataTaskDefinition.Create("SiblingExit");

            // start 有两条分支：b1(Trigger/Go→done) 与 b2(Other/Go→done2)。取 b1 应让 b2 停用并发其 End 事件。
            var start = new QuestState("start");
            var b1 = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            b1.AddTask(new CompleteDataTaskQuestTask(trigger, "Go", 1));
            var b2 = new QuestBranch { Id = "b2", DestinationStateId = "done2" };
            b2.AddTask(new CompleteDataTaskQuestTask(other, "Go", 1));
            b2.AddEvent(new CompleteDataTaskEvent(siblingExit, "B2") { Runtime = EEventRuntime.End });
            start.AddBranch(b1);
            start.AddBranch(b2);
            var done = new QuestState("done", EStateNodeType.Success);
            var done2 = new QuestState("done2", EStateNodeType.Success);
            var asset = QuestAsset.Create("q_sibling", "start", new List<QuestState> { start, done, done2 });

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);
                host.CompleteDataTask("Trigger", "Go"); // 取 b1 → 离开 start → b2 停用
                Assert.AreEqual(1, host.MasterTasks.GetCount("siblingexit_b2"),
                    "另一分支被取用、离开状态时，未取用的兄弟分支应触发一次其 End 事件");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TakingBranch_StillBroadcastsTaskCompleted_AfterBranchDeactivated()
        {
            // 回归保护：分支 Deactivate 不能清空 owning quest 引用，否则取分支后的 TaskCompleted 广播会丢。
            var trigger = DataTaskDefinition.Create("Trigger");
            var exit = DataTaskDefinition.Create("Exit");

            var asset = BuildQuest(trigger, out var b1, out _, out _);
            b1.AddEvent(new CompleteDataTaskEvent(exit, "Branch") { Runtime = EEventRuntime.End });

            var host = MakeHost(out var go);
            try
            {
                int taskCompleted = 0;
                host.QuestTaskCompleted += (_, __, ___) => taskCompleted++;

                host.BeginQuest(asset);
                host.CompleteDataTask("Trigger", "Go");

                Assert.AreEqual(1, host.MasterTasks.GetCount("exit_branch"), "End 事件应触发一次");
                Assert.AreEqual(1, taskCompleted, "取分支后仍应广播一次 QuestTaskCompleted");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BranchEntryEvent_RefireOnLoad_False_NotReplayed_True_Replayed()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var reward = DataTaskDefinition.Create("Reward"); // 一次性
            var banner = DataTaskDefinition.Create("Banner"); // 每次激活都重放

            var asset = BuildQuest(trigger, out var b1, out _, out _);
            b1.AddEvent(new CompleteDataTaskEvent(reward, "Gold") { RefireOnLoad = false }); // Start，一次性
            b1.AddEvent(new CompleteDataTaskEvent(banner, "Shown"));                          // Start，默认重放

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                // A：开始任务但不完成——分支 b1 激活一次，两事件各触发一次；存档时仍停在 start。
                hostA.BeginQuest(asset);
                Assert.AreEqual(1, hostA.MasterTasks.GetCount("reward_gold"));
                Assert.AreEqual(1, hostA.MasterTasks.GetCount("banner_shown"));

                var data = NarrativeSaveManager.FromJson(NarrativeSaveManager.ToJson(hostA.CaptureNarrativeState()));
                hostB.RestoreNarrativeState(data, new[] { asset }); // 读档重入 start → 重新激活 b1

                Assert.AreEqual(1, hostB.MasterTasks.GetCount("reward_gold"),
                    "RefireOnLoad=false 的分支 Start 事件读档时不应重放");
                Assert.AreEqual(2, hostB.MasterTasks.GetCount("banner_shown"),
                    "RefireOnLoad=true 的分支 Start 事件读档时应重放");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }
    }
}
