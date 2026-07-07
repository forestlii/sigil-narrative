// EditMode 测试：宿主的任务管理（BeginQuest/ForgetQuest/RestartQuest + 查询 + 事件桥接 + 模板克隆隔离）。
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class QuestHostEditTests
    {
        // ---------------- 构造辅助（反射写私有序列化字段，对齐 QuestEditTests 的做法）----------------

        private static DataTaskDefinition MakeDataTask(string taskName)
        {
            var t = ScriptableObject.CreateInstance<DataTaskDefinition>();
            typeof(DataTaskDefinition)
                .GetField("taskName", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(t, taskName);
            return t;
        }

        private static QuestAsset MakeQuestAsset(string startId, params QuestState[] states)
        {
            var asset = ScriptableObject.CreateInstance<QuestAsset>();
            typeof(QuestAsset).GetField("startStateId", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asset, startId);
            typeof(QuestAsset).GetField("states", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(asset, new List<QuestState>(states));
            return asset;
        }

        // start --b1[ CompleteDataTask(dataTask,arg) x required ]--> end(endType)
        private static QuestAsset BuildDataTaskQuestAsset(EStateNodeType endType, DataTaskDefinition dataTask, string argument, int required)
        {
            var start = new QuestState("start");
            var branch = new QuestBranch { Id = "b1", DestinationStateId = "end" };
            branch.AddTask(new CompleteDataTaskQuestTask(dataTask, argument, required));
            start.AddBranch(branch);
            var end = new QuestState("end", endType);
            return MakeQuestAsset("start", start, end);
        }

        private static NarrativeComponent MakeHost(out GameObject go)
        {
            go = new GameObject("host");
            return go.AddComponent<NarrativeComponent>();
        }

        // ---------------- 测试 ----------------

        [Test]
        public void BeginQuest_ClonesTemplate_StartsInProgress_AndIsQueryable()
        {
            var host = MakeHost(out var go);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 1);

                var quest = host.BeginQuest(asset);

                Assert.IsNotNull(quest);
                Assert.AreSame(asset, quest.SourceAsset, "运行实例应回指来源资产");
                Assert.AreSame(quest, host.GetQuestInstance(asset), "按资产应能取回同一实例");
                Assert.IsTrue(host.IsQuestInProgress(asset));
                Assert.IsTrue(host.IsQuestStartedOrFinished(asset));
                Assert.IsFalse(host.IsQuestFinished(asset));
                Assert.AreEqual(1, host.AllQuests.Count);
                // 未被运行改动：模板状态仍是干净的（克隆而非引用）
                Assert.AreNotSame(quest.CurrentState, asset.States[0], "运行状态应是克隆副本，非模板对象");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BeginQuest_SameAssetTwice_ReturnsNull_AndWarns()
        {
            var host = MakeHost(out var go);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 1);
                host.BeginQuest(asset);

                LogAssert.Expect(LogType.Warning, new Regex("BeginQuest"));
                var second = host.BeginQuest(asset);

                Assert.IsNull(second, "同一资产已在进行时，再次 BeginQuest 应返回 null");
                Assert.AreEqual(1, host.AllQuests.Count);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DataTaskCompletion_DrivesQuest_AndBridgesHostEvents()
        {
            var host = MakeHost(out var go);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 2);

                var events = new List<string>();
                host.QuestStarted += _ => events.Add("started");
                host.QuestNewState += (_, s) => events.Add("state:" + s.Id);
                host.QuestTaskProgressChanged += (_, _b, _t, o, n) => events.Add($"progress:{o}->{n}");
                host.QuestTaskCompleted += (_, _b, _t) => events.Add("taskDone");
                host.QuestBranchCompleted += (_, b) => events.Add("branch:" + b.Id);
                host.QuestSucceeded += _ => events.Add("succeeded");

                host.BeginQuest(asset);
                CollectionAssert.Contains(events, "started");
                CollectionAssert.Contains(events, "state:start");

                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestInProgress(asset), "1/2 仍进行中");

                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestSucceeded(asset), "2/2 应成功");

                CollectionAssert.Contains(events, "branch:b1");
                CollectionAssert.Contains(events, "state:end");
                CollectionAssert.Contains(events, "taskDone");
                CollectionAssert.Contains(events, "succeeded");
                CollectionAssert.Contains(events, "progress:0->1");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ForgetQuest_Removes_FiresEvent_AndAllowsFreshRebegin()
        {
            var host = MakeHost(out var go);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 2);
                host.BeginQuest(asset);
                host.CompleteDataTask("TalkTo", "Elder"); // 进度 1/2

                var forgotten = false;
                host.QuestForgotten += _ => forgotten = true;

                Assert.IsTrue(host.ForgetQuest(asset));
                Assert.IsTrue(forgotten);
                Assert.IsNull(host.GetQuestInstance(asset));
                Assert.IsFalse(host.IsQuestInProgress(asset));
                Assert.AreEqual(0, host.AllQuests.Count);

                // 遗忘后旧任务的 data-task 订阅应已断开：此刻再完成一次不应“复活”任何进度。
                host.CompleteDataTask("TalkTo", "Elder");

                // 重新开始 → 全新进度：需要从 0 重新累计 2 次才成功（证明是克隆而非复用旧进度）。
                var q2 = host.BeginQuest(asset);
                Assert.IsNotNull(q2);
                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestInProgress(asset), "重开后仅 1/2，仍进行中（进度确实清零）");
                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestSucceeded(asset));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RestartQuest_FiresRestartedWithOldInstance_AndResetsProgress()
        {
            var host = MakeHost(out var go);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 2);
                var q1 = host.BeginQuest(asset);
                host.CompleteDataTask("TalkTo", "Elder"); // 进度 1/2

                Quest restartedOld = null;
                host.QuestRestarted += q => restartedOld = q;

                Assert.IsTrue(host.RestartQuest(asset));
                Assert.AreSame(q1, restartedOld, "OnQuestRestarted 应携带被替换的旧实例");

                var q2 = host.GetQuestInstance(asset);
                Assert.IsNotNull(q2);
                Assert.AreNotSame(q1, q2, "重启应产生新实例");
                Assert.IsTrue(host.IsQuestInProgress(asset));

                // 新实例进度清零：1 次不够，2 次才成功。
                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestInProgress(asset), "重启后仅 1/2");
                host.CompleteDataTask("TalkTo", "Elder");
                Assert.IsTrue(host.IsQuestSucceeded(asset));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RestartAndForget_ReturnFalse_WhenQuestNotStarted()
        {
            var host = MakeHost(out var go);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 1);
                Assert.IsFalse(host.RestartQuest(asset), "未开始的任务不能重启");
                Assert.IsFalse(host.ForgetQuest(asset), "未开始的任务不能遗忘");
                Assert.IsNull(host.BeginQuest(null), "空资产应返回 null");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TwoHosts_ShareAsset_ButProgressIsolated_ProvingTemplatePurity()
        {
            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                var asset = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 1);

                hostA.BeginQuest(asset);
                hostB.BeginQuest(asset);

                // 只在 A 上完成 data-task：A 成功，B 不受影响（各自克隆、各自订阅自己的宿主）。
                hostA.CompleteDataTask("TalkTo", "Elder");

                Assert.IsTrue(hostA.IsQuestSucceeded(asset));
                Assert.IsTrue(hostB.IsQuestInProgress(asset), "另一宿主不应被串扰（模板未被运行改动）");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void GetInProgressAndSucceededQuests_ReflectState()
        {
            var host = MakeHost(out var go);
            try
            {
                var assetA = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("TalkTo"), "Elder", 1);
                var assetB = BuildDataTaskQuestAsset(EStateNodeType.Success, MakeDataTask("Obtain"), "Sword", 1);

                host.BeginQuest(assetA);
                host.BeginQuest(assetB);
                Assert.AreEqual(2, host.GetInProgressQuests().Count);
                Assert.AreEqual(0, host.GetSucceededQuests().Count);

                host.CompleteDataTask("TalkTo", "Elder"); // 只完成 A
                Assert.AreEqual(1, host.GetInProgressQuests().Count);
                Assert.AreEqual(1, host.GetSucceededQuests().Count);
                Assert.AreSame(assetB, host.GetInProgressQuests()[0].SourceAsset);
                Assert.AreSame(assetA, host.GetSucceededQuests()[0].SourceAsset);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
