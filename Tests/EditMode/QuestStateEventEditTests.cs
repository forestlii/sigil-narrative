// EditMode 测试：任务状态事件（进入发 Start、离开发 End、终止态也发）+ RefireOnLoad 读档过滤。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class QuestStateEventEditTests
    {
        private static NarrativeComponent MakeHost(out GameObject go)
        {
            go = new GameObject("host");
            return go.AddComponent<NarrativeComponent>();
        }

        // start(可带事件) --b1[ 完成 Trigger/Go x1 ]--> done(Success, 可带事件)
        private static QuestAsset BuildQuest(
            DataTaskDefinition trigger,
            out QuestState start,
            out QuestState done)
        {
            start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new CompleteDataTaskQuestTask(trigger, "Go", 1));
            start.AddBranch(branch);
            done = new QuestState("done", EStateNodeType.Success);
            // 注意：states 会在 BeginQuest 时被克隆；事件是只读配置，随克隆共享。
            return QuestAsset.Create("q_state_events", "start", new List<QuestState> { start, done });
        }

        [Test]
        public void StateEntryEvent_FiresOnEnteringState_IncludingTerminalSuccess()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var entered = DataTaskDefinition.Create("Entered");
            var reward = DataTaskDefinition.Create("Reward");

            var asset = BuildQuest(trigger, out var start, out var done);
            start.AddEvent(new CompleteDataTaskEvent(entered, "Start"));   // 进入 start 时触发
            done.AddEvent(new CompleteDataTaskEvent(reward, "Gold"));      // 进入 done(成功态) 时触发

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);
                Assert.AreEqual(1, host.MasterTasks.GetCount("entered_start"), "进入起始状态应触发其进入事件");
                Assert.AreEqual(0, host.MasterTasks.GetCount("reward_gold"), "尚未到达 done，奖励事件不应触发");

                host.CompleteDataTask("Trigger", "Go"); // 达标 → 转到 done(Success)
                Assert.IsTrue(host.IsQuestSucceeded(asset));
                Assert.AreEqual(1, host.MasterTasks.GetCount("reward_gold"), "到达终止成功态应触发其进入事件");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StateExitEvent_FiresOnLeavingState()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var left = DataTaskDefinition.Create("Left");

            var asset = BuildQuest(trigger, out var start, out _);
            start.AddEvent(new CompleteDataTaskEvent(left, "Start") { Runtime = EEventRuntime.End }); // 离开 start 时触发

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);
                Assert.AreEqual(0, host.MasterTasks.GetCount("left_start"), "还没离开 start");

                host.CompleteDataTask("Trigger", "Go"); // 离开 start → End 事件
                Assert.AreEqual(1, host.MasterTasks.GetCount("left_start"), "离开状态应触发其 End 事件");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RefireOnLoad_False_NotReExecuted_True_ReExecuted()
        {
            var trigger = DataTaskDefinition.Create("Trigger");
            var reward = DataTaskDefinition.Create("Reward");   // 一次性发放
            var banner = DataTaskDefinition.Create("Banner");   // 每次到达都重放

            var asset = BuildQuest(trigger, out _, out var done);
            done.AddEvent(new CompleteDataTaskEvent(reward, "Gold") { RefireOnLoad = false }); // 一次性
            done.AddEvent(new CompleteDataTaskEvent(banner, "Shown"));                          // 默认 RefireOnLoad=true

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                // A：玩到成功，两事件各触发一次。
                hostA.BeginQuest(asset);
                hostA.CompleteDataTask("Trigger", "Go");
                Assert.IsTrue(hostA.IsQuestSucceeded(asset));
                Assert.AreEqual(1, hostA.MasterTasks.GetCount("reward_gold"));
                Assert.AreEqual(1, hostA.MasterTasks.GetCount("banner_shown"));

                // 存档 → JSON 往返 → 全新宿主读档。
                var data = NarrativeSaveManager.FromJson(NarrativeSaveManager.ToJson(hostA.CaptureNarrativeState()));
                hostB.RestoreNarrativeState(data, new[] { asset });

                Assert.IsTrue(hostB.IsQuestSucceeded(asset));
                // 读档时重入 done 状态：一次性奖励不重放（仍 1），banner 重放（1→2）。
                Assert.AreEqual(1, hostB.MasterTasks.GetCount("reward_gold"),
                    "RefireOnLoad=false 的一次性事件读档时不应重放（否则会重复发奖励）");
                Assert.AreEqual(2, hostB.MasterTasks.GetCount("banner_shown"),
                    "RefireOnLoad=true 的事件读档时应重放");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void StateEntryEvents_AreNotBroadcastAsHostQuestEvents_DuringLoad()
        {
            // 读档静默：BeginForLoad 期间宿主级 OnQuest* 不广播（沿用 M4 的 _isLoading 抑制）。
            var trigger = DataTaskDefinition.Create("Trigger");
            var reward = DataTaskDefinition.Create("Reward");

            var asset = BuildQuest(trigger, out _, out var done);
            done.AddEvent(new CompleteDataTaskEvent(reward, "Gold") { RefireOnLoad = true });

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                hostA.BeginQuest(asset);
                hostA.CompleteDataTask("Trigger", "Go");
                var data = hostA.CaptureNarrativeState();

                int succeeded = 0, newState = 0;
                hostB.QuestSucceeded += _ => succeeded++;
                hostB.QuestNewState += (_, __) => newState++;

                hostB.RestoreNarrativeState(data, new[] { asset });

                Assert.AreEqual(0, succeeded, "读档不应广播 QuestSucceeded");
                Assert.AreEqual(0, newState, "读档不应广播 QuestNewState");
                // 但 RefireOnLoad=true 的状态事件仍执行了（奖励从存档的 1 重放到 2）。
                Assert.AreEqual(2, hostB.MasterTasks.GetCount("reward_gold"));
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }
    }
}
