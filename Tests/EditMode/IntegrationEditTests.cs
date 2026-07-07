// EditMode 测试：M5 集成——端到端全链路（对话事件→data-task→quest 推进→成功→存档→读档还原）
// 以及 tick 驱动的轮询任务（NarrativeComponent.TickActiveTasks / QuestTask.DriveTick）。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class IntegrationEditTests
    {
        // ---------------- 辅助 ----------------

        private static DataTaskDefinition MakeDataTask(string taskName)
        {
            // 用公开工厂构造（顺带覆盖 DataTaskDefinition.Create）。
            return DataTaskDefinition.Create(taskName);
        }

        private static QuestAsset MakeQuestAsset(string questId, string startId, params QuestState[] states)
        {
            // 用公开工厂构造（顺带覆盖 QuestAsset.Create）。
            return QuestAsset.Create(questId, startId, states);
        }

        private static NarrativeComponent MakeHost(out GameObject go)
        {
            go = new GameObject("host");
            return go.AddComponent<NarrativeComponent>();
        }

        // 自动推进的对话呈现器：每行立即推进、有选项选第一个。
        private sealed class AutoPresenter : IDialoguePresenter
        {
            private DialogueController _c;
            public void OnDialogueBegan(DialogueController controller) => _c = controller;
            public void OnLineStarted(DialogueNode node, DialogueLine line) => _c.AdvanceLine();
            public void OnLineFinished(DialogueNode node, DialogueLine line) { }
            public void OnResponsesAvailable(IReadOnlyList<DialogueNode_Player> options)
            {
                if (options.Count > 0) _c.SelectOption(options[0]);
            }
            public void OnDialogueEnded(DialogueController controller) { }
        }

        // 每次 Tick 推进 1 点进度的轮询任务（测 tick 驱动）。
        private sealed class TickCountTask : QuestTask
        {
            public TickCountTask(int required, float interval)
            {
                SetRequiredQuantityForConstruction(required);
                SetTickIntervalForConstruction(interval);
            }

            public override void Tick() => AddProgress(1);
        }

        // ---------------- 端到端 ----------------

        [Test]
        public void EndToEnd_DialogueEventCompletesDataTask_DrivesQuest_ThenSaveRestore()
        {
            var talk = MakeDataTask("TalkTo");

            // 任务：start --b1[ 完成 talkto_elder x1 ]--> done(Success)
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new CompleteDataTaskQuestTask(talk, "Elder", 1));
            start.AddBranch(branch);
            var done = new QuestState("done", EStateNodeType.Success);
            var asset = MakeQuestAsset("q_e2e", "start", start, done);

            // 对话：一句 NPC 台词，到达时触发“完成 talkto_elder”事件；无后续 → 说完即结束。
            var graph = new DialogueGraph { RootId = "npc" };
            var npc = new DialogueNode_NPC("npc", new DialogueLine("Take this, adventurer."));
            npc.AddEvent(new CompleteDataTaskEvent(talk, "Elder"));
            graph.AddNode(npc);

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                // 起任务（任务项订阅宿主 data-task 事件）。
                hostA.BeginQuest(asset);
                Assert.IsTrue(hostA.IsQuestInProgress(asset));

                // 播对话：NPC 行开始 → 事件完成 data-task → 任务项 +1 → 达标 → 任务成功。
                var controller = new DialogueController(graph, hostA.MakeContext(), new AutoPresenter());
                Assert.IsTrue(controller.Begin());

                Assert.AreEqual(1, hostA.MasterTasks.GetCount("talkto_elder"), "对话事件应完成一次 data-task");
                Assert.IsTrue(hostA.IsQuestSucceeded(asset), "data-task 完成应把任务推进到成功");

                // 存档 → JSON 往返 → 全新宿主读档 → 仍为成功。
                var data = NarrativeSaveManager.FromJson(NarrativeSaveManager.ToJson(hostA.CaptureNarrativeState()));
                Assert.IsTrue(hostB.RestoreNarrativeState(data, new[] { asset }));
                Assert.IsTrue(hostB.IsQuestSucceeded(asset), "读档后任务仍为成功");
                Assert.AreEqual(1, hostB.MasterTasks.GetCount("talkto_elder"), "读档后 data-task 记录还原");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        // ---------------- tick 驱动 ----------------

        [Test]
        public void TickActiveTasks_DrivesPollingTaskToCompletion()
        {
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new TickCountTask(required: 3, interval: 1f));
            start.AddBranch(branch);
            var done = new QuestState("done", EStateNodeType.Success);
            var asset = MakeQuestAsset("q_tick", "start", start, done);

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);

                host.TickActiveTasks(1f);
                Assert.IsTrue(host.IsQuestInProgress(asset), "1/3");
                host.TickActiveTasks(1f);
                Assert.IsTrue(host.IsQuestInProgress(asset), "2/3");
                host.TickActiveTasks(1f);
                Assert.IsTrue(host.IsQuestSucceeded(asset), "3/3 → 成功");

                // 完成后再 tick 不应报错、不改变结果。
                host.TickActiveTasks(5f);
                Assert.IsTrue(host.IsQuestSucceeded(asset));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TickActiveTasks_AccumulatesFractionalDelta_AndCatchesUp()
        {
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new TickCountTask(required: 2, interval: 1f));
            start.AddBranch(branch);
            var done = new QuestState("done", EStateNodeType.Success);
            var asset = MakeQuestAsset("q_frac", "start", start, done);

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);

                // 半间隔多次累积：0.5+0.5=1 → 一次 tick。
                host.TickActiveTasks(0.5f);
                Assert.IsTrue(host.IsQuestInProgress(asset));
                host.TickActiveTasks(0.5f);
                Assert.IsTrue(host.IsQuestInProgress(asset), "累计到 1 个间隔 → 1/2");

                // 一帧给足 2 个间隔 → 单帧补 2 次 tick，直接达标。
                host.TickActiveTasks(2f);
                Assert.IsTrue(host.IsQuestSucceeded(asset), "单帧大 dt 应补 tick 到 2/2");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TickActiveTasks_DoesNotTick_ZeroIntervalTasks()
        {
            var talk = MakeDataTask("TalkTo");
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new CompleteDataTaskQuestTask(talk, "Elder", 1)); // TickInterval 默认 0
            start.AddBranch(branch);
            var done = new QuestState("done", EStateNodeType.Success);
            var asset = MakeQuestAsset("q_notick", "start", start, done);

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);
                host.TickActiveTasks(100f); // 大量流逝也不该推进非轮询任务
                Assert.IsTrue(host.IsQuestInProgress(asset), "TickInterval=0 的任务不应被 tick 推进");

                // 仍可正常由 data-task 事件驱动。
                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestSucceeded(asset));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TickActiveTasks_IgnoresNonPositiveDelta()
        {
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new TickCountTask(required: 1, interval: 1f));
            start.AddBranch(branch);
            var done = new QuestState("done", EStateNodeType.Success);
            var asset = MakeQuestAsset("q_zero", "start", start, done);

            var host = MakeHost(out var go);
            try
            {
                host.BeginQuest(asset);
                host.TickActiveTasks(0f);
                host.TickActiveTasks(-1f);
                Assert.IsTrue(host.IsQuestInProgress(asset), "dt<=0 不应驱动任何 tick");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
