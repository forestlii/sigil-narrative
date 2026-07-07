// EditMode 测试：叙事存档（DTO JSON 往返 / 宿主 Capture→Restore 全链路 / 读档静默 / 读档后任务仍活 /
// SaveManager 注入假文件系统 / 未知任务跳过）。
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class SaveEditTests
    {
        // ---------------- 构造辅助 ----------------

        private static void SetPrivate(object target, string field, object value)
        {
            target.GetType()
                .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }

        private static DataTaskDefinition MakeDataTask(string taskName)
        {
            var t = ScriptableObject.CreateInstance<DataTaskDefinition>();
            SetPrivate(t, "taskName", taskName);
            return t;
        }

        private static QuestAsset MakeQuestAsset(string questId, string startId, params QuestState[] states)
        {
            var asset = ScriptableObject.CreateInstance<QuestAsset>();
            SetPrivate(asset, "questId", questId);
            SetPrivate(asset, "startStateId", startId);
            SetPrivate(asset, "states", new List<QuestState>(states));
            return asset;
        }

        // start --b1[ TalkTo/Elder x2 ]--> mid --b2[ Obtain/Herb x2 ]--> done(Success)
        private static QuestAsset MakeTwoStepQuest(DataTaskDefinition talk, DataTaskDefinition obtain)
        {
            var start = new QuestState("start");
            var b1 = new QuestBranch { Id = "b1", DestinationStateId = "mid" };
            b1.AddTask(new CompleteDataTaskQuestTask(talk, "Elder", 2));
            start.AddBranch(b1);

            var mid = new QuestState("mid");
            var b2 = new QuestBranch { Id = "b2", DestinationStateId = "done" };
            b2.AddTask(new CompleteDataTaskQuestTask(obtain, "Herb", 2));
            mid.AddBranch(b2);

            var done = new QuestState("done", EStateNodeType.Success);
            return MakeQuestAsset("q_two_step", "start", start, mid, done);
        }

        private static NarrativeComponent MakeHost(out GameObject go)
        {
            go = new GameObject("host");
            return go.AddComponent<NarrativeComponent>();
        }

        private sealed class FakeFileSystem : IFileSystem
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public bool Exists(string path) => Files.ContainsKey(path);
            public string ReadAllText(string path) => Files[path];
            public void WriteAllText(string path, string contents) => Files[path] = contents;
        }

        // ---------------- DTO / JSON ----------------

        [Test]
        public void SaveData_JsonRoundTrip_PreservesAllFields()
        {
            var data = new NarrativeSaveData();
            data.masterTasks.Add(new SavedMasterTask("talkto_elder", 2));
            var q = new SavedQuest { questId = "q1", currentStateId = "mid" };
            q.branches.Add(new SavedQuestBranch("b1", new List<int> { 2 }));
            q.branches.Add(new SavedQuestBranch("b2", new List<int> { 1 }));
            q.reachedStateIds.Add("start");
            q.reachedStateIds.Add("mid");
            data.quests.Add(q);

            var json = NarrativeSaveManager.ToJson(data);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var back = NarrativeSaveManager.FromJson(json);
            Assert.AreEqual(1, back.masterTasks.Count);
            Assert.AreEqual("talkto_elder", back.masterTasks[0].task);
            Assert.AreEqual(2, back.masterTasks[0].count);
            Assert.AreEqual(1, back.quests.Count);
            Assert.AreEqual("q1", back.quests[0].questId);
            Assert.AreEqual("mid", back.quests[0].currentStateId);
            Assert.AreEqual(2, back.quests[0].branches.Count);
            Assert.AreEqual("b2", back.quests[0].branches[1].branchId);
            Assert.AreEqual(1, back.quests[0].branches[1].taskProgress[0]);
            CollectionAssert.AreEqual(new[] { "start", "mid" }, back.quests[0].reachedStateIds);
        }

        [Test]
        public void FromJson_EmptyOrNull_ReturnsNull()
        {
            Assert.IsNull(NarrativeSaveManager.FromJson(null));
            Assert.IsNull(NarrativeSaveManager.FromJson(""));
        }

        // ---------------- Capture → Restore 全链路 ----------------

        [Test]
        public void CaptureThenRestore_OnFreshHost_RestoresStateProgressReachedAndMasterTasks()
        {
            var talk = MakeDataTask("TalkTo");
            var obtain = MakeDataTask("Obtain");
            var asset = MakeTwoStepQuest(talk, obtain);

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                // 在 A 上：推进到 mid（b1 完成），并在 b2 上做 1/2 进度。
                hostA.BeginQuest(asset);
                hostA.CompleteDataTask("TalkTo", "Elder");
                hostA.CompleteDataTask("TalkTo", "Elder"); // b1 达标 → 进入 mid
                Assert.AreEqual("mid", hostA.GetQuestInstance(asset).CurrentState.Id);
                hostA.CompleteDataTask("Obtain", "Herb");  // b2: 1/2

                var data = hostA.CaptureNarrativeState();

                // 往返一次 JSON，确保存档能落盘再读回。
                data = NarrativeSaveManager.FromJson(NarrativeSaveManager.ToJson(data));

                // 在全新的 B 上还原。
                Assert.IsTrue(hostB.RestoreNarrativeState(data, new[] { asset }));

                // data-task 表还原。
                Assert.AreEqual(2, hostB.MasterTasks.GetCount("talkto_elder"));
                Assert.AreEqual(1, hostB.MasterTasks.GetCount("obtain_herb"));

                // 任务状态/reached 还原。
                var q = hostB.GetQuestInstance(asset);
                Assert.IsNotNull(q);
                Assert.IsTrue(hostB.IsQuestInProgress(asset));
                Assert.AreEqual("mid", q.CurrentState.Id);
                var reachedIds = new List<string>();
                foreach (var s in q.ReachedStates) reachedIds.Add(s.Id);
                CollectionAssert.AreEqual(new[] { "start", "mid" }, reachedIds);

                // 关键：b2 进度确实回填成 1/2 —— 再完成一次即成功（若没回填则只有 1/2，不会成功）。
                hostB.CompleteDataTask("Obtain", "Herb");
                Assert.IsTrue(hostB.IsQuestSucceeded(asset), "b2 应从 1/2 回填，+1 后达标成功（证明进度已还原且任务仍活）");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void Restore_DoesNotBroadcastHostQuestEvents()
        {
            var talk = MakeDataTask("TalkTo");
            var obtain = MakeDataTask("Obtain");
            var asset = MakeTwoStepQuest(talk, obtain);

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                hostA.BeginQuest(asset);
                hostA.CompleteDataTask("TalkTo", "Elder");
                var data = hostA.CaptureNarrativeState();

                int started = 0, newState = 0, progress = 0, succeeded = 0;
                hostB.QuestStarted += _ => started++;
                hostB.QuestNewState += (_, __) => newState++;
                hostB.QuestTaskProgressChanged += (_, __, ___, ____, _____) => progress++;
                hostB.QuestSucceeded += _ => succeeded++;

                hostB.RestoreNarrativeState(data, new[] { asset });

                Assert.AreEqual(0, started, "读档不应广播 QuestStarted");
                Assert.AreEqual(0, newState, "读档不应广播 QuestNewState");
                Assert.AreEqual(0, progress, "读档不应广播进度事件");
                Assert.AreEqual(0, succeeded, "读档不应广播 QuestSucceeded");
                Assert.IsFalse(hostB.IsLoading, "读档结束后 IsLoading 应复位");

                // 读档后正常游戏事件应恢复广播。
                hostB.QuestTaskProgressChanged += (_, __, ___, ____, _____) => { };
                hostB.CompleteDataTask("TalkTo", "Elder"); // b1 达标 → 进入 mid
                Assert.Greater(newState, 0, "读档后真实进度应重新广播");
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void Restore_FinishedQuest_ComesBackAsSucceeded()
        {
            var talk = MakeDataTask("TalkTo");
            var obtain = MakeDataTask("Obtain");
            var asset = MakeTwoStepQuest(talk, obtain);

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                hostA.BeginQuest(asset);
                hostA.CompleteDataTask("TalkTo", "Elder");
                hostA.CompleteDataTask("TalkTo", "Elder");
                hostA.CompleteDataTask("Obtain", "Herb");
                hostA.CompleteDataTask("Obtain", "Herb"); // 全通 → Success
                Assert.IsTrue(hostA.IsQuestSucceeded(asset));

                var data = hostA.CaptureNarrativeState();
                hostB.RestoreNarrativeState(data, new[] { asset });

                Assert.IsTrue(hostB.IsQuestSucceeded(asset), "已成功的任务读档后仍为成功");
                Assert.AreEqual("done", hostB.GetQuestInstance(asset).CurrentState.Id);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void Restore_UnknownQuest_IsSkippedWithWarning()
        {
            var talk = MakeDataTask("TalkTo");
            var obtain = MakeDataTask("Obtain");
            var asset = MakeTwoStepQuest(talk, obtain);

            var hostA = MakeHost(out var goA);
            var hostB = MakeHost(out var goB);
            try
            {
                hostA.BeginQuest(asset);
                var data = hostA.CaptureNarrativeState();

                // 还原时不提供该资产 → 应跳过并警告。
                LogAssert.Expect(LogType.Warning, new Regex("q_two_step"));
                Assert.IsTrue(hostB.RestoreNarrativeState(data, new QuestAsset[0]));
                Assert.AreEqual(0, hostB.GetInProgressQuests().Count);
            }
            finally
            {
                Object.DestroyImmediate(goA);
                Object.DestroyImmediate(goB);
            }
        }

        [Test]
        public void Restore_NullData_ReturnsFalse()
        {
            var host = MakeHost(out var go);
            try
            {
                Assert.IsFalse(host.RestoreNarrativeState(null, null));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---------------- SaveManager + 注入文件系统 ----------------

        [Test]
        public void SaveManager_SaveThenLoad_ViaInjectedFileSystem_RoundTrips()
        {
            var fs = new FakeFileSystem();
            var mgr = new NarrativeSaveManager(fs);
            const string path = "slot0.json";

            Assert.IsNull(mgr.Load(path), "文件不存在应返回 null");
            Assert.IsFalse(mgr.Exists(path));

            var data = new NarrativeSaveData();
            data.masterTasks.Add(new SavedMasterTask("kill_wolf", 5));

            Assert.IsTrue(mgr.Save(path, data));
            Assert.IsTrue(mgr.Exists(path));
            Assert.IsTrue(fs.Files.ContainsKey(path), "应通过注入的文件系统写入");

            var loaded = mgr.Load(path);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.masterTasks.Count);
            Assert.AreEqual("kill_wolf", loaded.masterTasks[0].task);
            Assert.AreEqual(5, loaded.masterTasks[0].count);
        }

        [Test]
        public void SaveManager_SaveNull_ReturnsFalse_AndWritesNothing()
        {
            var fs = new FakeFileSystem();
            var mgr = new NarrativeSaveManager(fs);

            Assert.IsFalse(mgr.Save("slot0.json", null));
            Assert.AreEqual(0, fs.Files.Count);
        }
    }
}
