// EditMode 测试：验证 data-task 规范化与 MasterTaskList 累计/查询行为。
using NUnit.Framework;
using Likeon.Narrative;

namespace Likeon.Narrative.Tests
{
    public class CoreDataTaskEditTests
    {
        [Test]
        public void MakeTaskString_LowercasesAndStripsSpaces()
        {
            // 对齐 UE MakeTaskString：转小写 + 删空格，参数用下划线拼接。
            Assert.AreEqual("killnpc_king", DataTaskDefinition.Normalize("KillNPC", "King"));
            Assert.AreEqual("talkto_kingbob", DataTaskDefinition.Normalize("Talk To", "King Bob"),
                "任务名与参数里的空格都应被删除");
        }

        [Test]
        public void MakeTaskString_EmptyArgumentStillKeepsUnderscore()
        {
            // 即使参数为空也带下划线，与 UE 行为一致（防止 "kill" 与 "kill_" 混淆）。
            Assert.AreEqual("finditem_", DataTaskDefinition.Normalize("FindItem", ""));
            Assert.AreEqual("finditem_", DataTaskDefinition.Normalize("FindItem", null));
        }

        [Test]
        public void MasterTaskList_AccumulatesCompletionCount()
        {
            var list = new MasterTaskList();
            Assert.AreEqual(0, list.GetCount("killnpc_goblin"), "未完成时应为 0");

            Assert.AreEqual(1, list.CompleteTask("killnpc_goblin"));
            Assert.AreEqual(4, list.CompleteTask("killnpc_goblin", 3), "应累加而非覆盖");
            Assert.AreEqual(4, list.GetCount("killnpc_goblin"));
        }

        [Test]
        public void MasterTaskList_HasCompleted_RespectsThreshold()
        {
            var list = new MasterTaskList();
            list.CompleteTask("killnpc_goblin", 3);

            Assert.IsTrue(list.HasCompleted("killnpc_goblin"), "默认阈值 1，做过即为真");
            Assert.IsTrue(list.HasCompleted("killnpc_goblin", 3), "恰好达到阈值应为真");
            Assert.IsFalse(list.HasCompleted("killnpc_goblin", 4), "未达阈值应为假");
            Assert.IsFalse(list.HasCompleted("never_done"), "从未做过应为假");
        }

        [Test]
        public void MasterTaskList_RestoreEntry_SetsRawCount()
        {
            var list = new MasterTaskList();
            list.RestoreEntry("finditem_dragonstone", 2);

            Assert.AreEqual(2, list.GetCount("finditem_dragonstone"), "读档写入应绕过累加、直接置数");
            Assert.AreEqual(1, list.DistinctCount);
        }

        [Test]
        public void MasterTaskList_IgnoresEmptyKeys()
        {
            var list = new MasterTaskList();
            Assert.AreEqual(0, list.CompleteTask("", 5), "空任务串应被忽略");
            Assert.AreEqual(0, list.DistinctCount);
        }
    }
}
