// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 端到端样例：一个脚本走通「起任务 → 播对话 → 对话事件完成 data-task → 任务推进到成功 → 存档 → 读档还原」的整条链路。
// 为了“按下 Play 就能看”，这里全部用代码构建资产（QuestAsset.Create / DataTaskDefinition.Create）；
// 真实项目里你会在 Inspector 里把这些做成资产、拖进字段，而不是代码里建。
//
// 用法：新建空场景，建一个空 GameObject，挂上本组件，按 Play，看 Console 逐步输出。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative.Samples
{
    [AddComponentMenu("Sigil/Narrative/Samples/End-To-End Demo")]
    public sealed class NarrativeEndToEndDemo : MonoBehaviour
    {
        private void Start()
        {
            // 宿主：加到本 GameObject 上（无需继承基类）。
            var host = gameObject.AddComponent<NarrativeComponent>();

            // 1) 在代码里建一个数据任务、一个任务资产、一段对话。
            var talkTo = DataTaskDefinition.Create("TalkTo");
            var quest = BuildQuest(talkTo);
            var dialogue = BuildDialogue(talkTo);

            Log("=== Narrative end-to-end demo ===");

            // 2) 订阅宿主级任务事件，观察推进。
            host.QuestStarted += q => Log($"[quest] started: {q.SourceAsset.QuestId}");
            host.QuestNewState += (q, s) => Log($"[quest] new state: {s.Id}");
            host.QuestSucceeded += q => Log($"[quest] SUCCEEDED: {q.SourceAsset.QuestId}");

            // 3) 起任务。
            host.BeginQuest(quest);
            Log($"[quest] in progress? {host.IsQuestInProgress(quest)}");

            // 4) 播对话——NPC 那句台词到达时会触发 CompleteDataTaskEvent，从而完成 talkto_elder，推进任务。
            Log("[dialogue] playing…");
            var controller = new DialogueController(dialogue, host.MakeContext(), new LoggingPresenter());
            controller.Begin();

            Log($"[quest] succeeded after dialogue? {host.IsQuestSucceeded(quest)}");
            Log($"[data-task] talkto_elder count = {host.MasterTasks.GetCount("talkto_elder")}");

            // 5) 存档到磁盘。
            var manager = new NarrativeSaveManager();
            var path = System.IO.Path.Combine(Application.persistentDataPath, "narrative_demo.json");
            manager.Save(path, host.CaptureNarrativeState());
            Log($"[save] wrote {path}");

            // 6) 模拟“重启”：在一个全新宿主上读档还原。
            var freshGo = new GameObject("FreshHost");
            var freshHost = freshGo.AddComponent<NarrativeComponent>();
            var loaded = manager.Load(path);
            freshHost.RestoreNarrativeState(loaded, new[] { quest });
            Log($"[load] restored — quest succeeded? {freshHost.IsQuestSucceeded(quest)}, " +
                $"talkto_elder = {freshHost.MasterTasks.GetCount("talkto_elder")}");

            Log("=== demo complete ===");
        }

        // start --b1[ 完成 talkto_elder x1 ]--> done(Success)
        private static QuestAsset BuildQuest(DataTaskDefinition talkTo)
        {
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new CompleteDataTaskQuestTask(talkTo, "Elder", 1));
            start.AddBranch(branch);
            var done = new QuestState("done", EStateNodeType.Success);
            return QuestAsset.Create("DemoQuest", "start", new List<QuestState> { start, done });
        }

        // 一句 NPC 台词，到达时触发“完成 talkto_elder”事件；无后续 → 说完即结束。
        private static DialogueGraph BuildDialogue(DataTaskDefinition talkTo)
        {
            var graph = new DialogueGraph { RootId = "npc_hello" };
            var npc = new DialogueNode_NPC("npc_hello", new DialogueLine("Well met. Consider it done."), "elder");
            npc.AddEvent(new CompleteDataTaskEvent(talkTo, "Elder"));
            graph.AddNode(npc);
            return graph;
        }

        private static void Log(string message) => Debug.Log(message);

        // 自动推进的对话呈现器：每行立即推进、有选项选第一个，并把台词打到 Console。
        private sealed class LoggingPresenter : IDialoguePresenter
        {
            private DialogueController _c;
            public void OnDialogueBegan(DialogueController controller) => _c = controller;
            public void OnLineStarted(DialogueNode node, DialogueLine line)
            {
                Debug.Log($"[dialogue] {node.Id}: \"{line.Text}\"");
                _c.AdvanceLine();
            }
            public void OnLineFinished(DialogueNode node, DialogueLine line) { }
            public void OnResponsesAvailable(IReadOnlyList<DialogueNode_Player> options)
            {
                if (options.Count > 0) _c.SelectOption(options[0]);
            }
            public void OnDialogueEnded(DialogueController controller) => Debug.Log("[dialogue] ended");
        }
    }
}
