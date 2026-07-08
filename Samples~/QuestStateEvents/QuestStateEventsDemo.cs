// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 样例：任务「状态事件」+ RefireOnLoad。演示——
//   · 怎么写自定义 NarrativeEvent，挂到任务状态上，在“到达该状态”时触发副作用（发奖励、弹横幅…）；
//   · RefireOnLoad 的区别：一次性发放(false)读档时不重放；需要重新应用的效果(true)读档时重放。
//
// 用法：新建空场景 → 空 GameObject → 挂本组件 → 按 Play → 看 Console。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative.Samples
{
    /// <summary>演示用的“钱包”（游戏自己的持久状态，不属于叙事存档）。</summary>
    public static class DemoWallet
    {
        public static int Gold;
    }

    /// <summary>
    /// 自定义状态事件：到达某状态时给玩家发金币。
    /// 因为金币记在游戏自己的存档里（不在叙事存档里），读档时**不该**再发一次 → <see cref="NarrativeEvent.RefireOnLoad"/>=false。
    /// </summary>
    [Serializable]
    public sealed class GrantGoldEvent : NarrativeEvent
    {
        [SerializeField] private int amount = 100;

        public GrantGoldEvent() { }

        public GrantGoldEvent(int amount)
        {
            this.amount = amount;
            RefireOnLoad = false; // 一次性发放：读档不重发
        }

        public override void Execute(NarrativeContext context)
        {
            DemoWallet.Gold += amount;
            Debug.Log($"[event] 发放 {amount} 金币（钱包现在 {DemoWallet.Gold}）");
        }

        public override string GetHintText() => $"(+{amount} 金币)";
    }

    /// <summary>
    /// 自定义状态事件：到达某状态时弹一条横幅（纯表现）。
    /// 横幅这类“需要重新应用的瞬时表现”读档时**应该**再来一次 → RefireOnLoad 用默认的 true。
    /// </summary>
    [Serializable]
    public sealed class ShowBannerEvent : NarrativeEvent
    {
        [SerializeField] private string text = "任务完成！";

        public ShowBannerEvent() { }
        public ShowBannerEvent(string text) { this.text = text; }

        public override void Execute(NarrativeContext context) => Debug.Log($"[event] 横幅：{text}");
    }

    [AddComponentMenu("Sigil/Narrative/Samples/Quest State Events Demo")]
    public sealed class QuestStateEventsDemo : MonoBehaviour
    {
        private void Start()
        {
            DemoWallet.Gold = 0;
            var host = gameObject.AddComponent<NarrativeComponent>();

            var trigger = DataTaskDefinition.Create("Trigger");
            var quest = BuildQuest(trigger);

            Log("=== 任务状态事件 demo ===");

            // 起任务 → 完成 data-task → 到达 done(成功态) → done 上的状态事件触发。
            host.BeginQuest(quest);
            Log("[quest] 已开始，去触发目标…");
            host.CompleteDataTask("Trigger", "Go");
            Log($"[quest] 成功？{host.IsQuestSucceeded(quest)} — 钱包 = {DemoWallet.Gold}");

            // 存档到磁盘（叙事状态；注意金币不在里面，是游戏自己的存档）。
            var manager = new NarrativeSaveManager();
            var path = System.IO.Path.Combine(Application.persistentDataPath, "narrative_state_events_demo.json");
            manager.Save(path, host.CaptureNarrativeState());
            Log($"[save] 已写入 {path}");

            // 模拟重启 + 读档：在全新宿主上还原。观察哪些状态事件重放、哪些不重放。
            Log("--- 模拟重启并读档 ---");
            var freshGo = new GameObject("FreshHost");
            var freshHost = freshGo.AddComponent<NarrativeComponent>();
            freshHost.RestoreNarrativeState(manager.Load(path), new[] { quest });

            Log($"[load] 已还原 — 任务成功？{freshHost.IsQuestSucceeded(quest)}；钱包 = {DemoWallet.Gold}");
            Log("↑ 横幅事件(RefireOnLoad=true)读档时重放了；发金币事件(RefireOnLoad=false)没有——钱包仍是 100，没重复发放。");
            Log("=== demo 结束 ===");
        }

        // start --b1[ 完成 trigger_go x1 ]--> done(Success + 两个状态进入事件)
        private static QuestAsset BuildQuest(DataTaskDefinition trigger)
        {
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
            branch.AddTask(new CompleteDataTaskQuestTask(trigger, "Go", 1));
            start.AddBranch(branch);

            var done = new QuestState("done", EStateNodeType.Success);
            done.AddEvent(new ShowBannerEvent("任务完成！")); // RefireOnLoad=true（默认）→ 读档重放
            done.AddEvent(new GrantGoldEvent(100));           // RefireOnLoad=false → 读档不重放

            return QuestAsset.Create("StateEventsDemoQuest", "start", new List<QuestState> { start, done });
        }

        private static void Log(string message) => Debug.Log(message);
    }
}
