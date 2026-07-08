[English](Usage.md) | [简体中文](Usage.zh-CN.md)

# Sigil Narrative — 使用指南

一份上手指南，讲清怎么用这套叙事框架：**数据任务（data-task）**、**条件/事件**节点模型、**对话**、
以及**任务（quest）状态机——全部通过同一个宿主组件驱动。

> **状态：`0.x`，开发中。** 图编辑器尚未发布（见[尚未包含的部分](#尚未包含的部分)）。
> 公开 API 未稳定，可能变动。

## 目录

1. [安装](#安装)
2. [核心概念速览](#核心概念速览)
3. [5 分钟快速上手](#5-分钟快速上手)
4. [数据任务与主任务表](#数据任务与主任务表)
5. [条件与事件（节点模型）](#条件与事件节点模型)
6. [对话](#对话)
7. [任务](#任务)
8. [存档与读档](#存档与读档)
9. [`NarrativeComponent` 速查表](#narrativecomponent-速查表)
10. [从 Narrative Pro / UE 迁移过来？](#从-narrative-pro--ue-迁移过来)
11. [尚未包含的部分](#尚未包含的部分)

---

## 安装

把 `com.likeon.narrative` 文件夹拷进项目的 `Packages/` 目录，或用
**Package Manager → Add package from disk…** 选中 `package.json`。它是独立的——没有其它依赖要装。

当包位于项目文件夹之外时，要跑内置的 EditMode 测试，需在 `Packages/manifest.json` 里把它加进
`"testables"`，然后打开 **Window → General → Test Runner**：

```json
"testables": [ "com.likeon.narrative" ]
```

## 核心概念速览

| 概念 | 类型 | 是什么 |
|---|---|---|
| **数据任务** | `DataTaskDefinition`（ScriptableObject） | 轻量的 `任务名_参数` 标记——“玩家做了 X”。本身无逻辑，会被规范化成一个原始串。 |
| **主任务表** | `MasterTaskList` | 持久记录：原始任务串 → 完成次数。对话与任务的耦合点。 |
| **宿主** | `NarrativeComponent`（`MonoBehaviour`，实现 `INarrativeHost`） | 唯一入口：完成 data-task、开始/查询任务、构造上下文。无需继承任何基类。 |
| **上下文** | `NarrativeContext` | 条件/事件收到的东西：`Host` + 可选的 `Target` GameObject。 |
| **条件 / 事件** | `NarrativeCondition` / `NarrativeEvent` | 挂在节点上、可复用、可序列化的逻辑——门控内容、触发副作用。 |
| **对话** | `DialogueGraph` + `DialogueController` + `IDialoguePresenter` | 扁平、按 ID 互引的节点图；控制器推进它；你的呈现器画出它。 |
| **任务** | `QuestAsset` → `Quest`（State / Branch / Task） | 状态机：分支里的任务全完成 → 取该分支 → 进入下一状态。 |

一句话心智模型：**data-task 记录发生了什么 → 条件读它、事件写它 → 对话和任务不过是承载条件/事件的节点图。**

## 5 分钟快速上手

```csharp
using Likeon.Narrative;
using UnityEngine;

public class Demo : MonoBehaviour
{
    [SerializeField] private DataTaskDefinition talkTo; // 一个 Data Task 资产，如 TaskName = "TalkTo"

    private void Start()
    {
        // 1. 宿主——加到任意 GameObject 上（不用继承基类）。
        var host = gameObject.AddComponent<NarrativeComponent>();

        // 2. 监听完成（规范化后的原始任务串，如 "talkto_elder"）。
        host.DataTaskCompleted += raw => Debug.Log($"完成: {raw}");

        // 3. 记录“玩家和长老对话过”。
        host.CompleteDataTask(talkTo, "Elder");

        // 4. 之后再查回来。
        bool met = host.MasterTasks.HasCompleted(talkTo, "Elder");   // true
        int times = host.MasterTasks.GetCount(talkTo, "Elder");      // 1
    }
}
```

用 **Assets → Create → Sigil → Narrative → Data Task** 创建 `DataTaskDefinition` 资产，设好
**Task Name**（如 `TalkTo`），拖进字段即可。对话和任务都建立在这一条循环之上。

## 数据任务与主任务表

一个**数据任务**就是一个规范化的字符串。`DataTaskDefinition.MakeTaskString(argument)` 会把
`任务名_参数` 转小写并删掉空格——`"Talk To" + "King Bob"` → `"talkto_kingbob"`。这与原引擎行为完全一致，
包括参数为空时也保留下划线（`"kill_"`）。

你几乎不需要自己拼这个串。通过宿主完成任务，它会写入 `MasterTaskList` 并广播：

```csharp
host.CompleteDataTask(task, "Elder");          // 用一个 DataTaskDefinition
host.CompleteDataTask("TalkTo", "Elder");      // 或直接按名字，免资产
host.CompleteDataTask(task, "Wolf", quantity: 3);
```

直接查询记录：

```csharp
MasterTaskList tasks = host.MasterTasks;
tasks.GetCount(task, "Wolf");        // 3
tasks.HasCompleted(task, "Wolf", 5); // false——目前只有 3 次
```

> 💡 主任务表是**存档核心原语**，也是系统间的桥：一个对话选项可以要求
> `HasCompleted(KillNPC, King)`，一个任务项可以在 `CompleteDataTask(TalkTo, Elder)` 触发时推进。
> 下面两处都会用到。

## 条件与事件（节点模型）

对话节点和任务状态共享一个基类 `NarrativeNodeBase`：一个 `Id`、一组**条件**（全部通过节点才可用）、
一组**事件**（到达节点时触发）。条件和事件都用 `[SerializeReference]` 多态——继承它们加你自己的，
会出现在 Inspector 的托管引用下拉里。

**条件**——继承 `NarrativeCondition`，覆写 `CheckCondition`。`Not` 翻转结果；调用方用 `IsMet`（已应用 `Not`）。

```csharp
[System.Serializable]
public sealed class HasGoldCondition : NarrativeCondition
{
    [SerializeField] private int minGold;
    public override bool CheckCondition(NarrativeContext ctx)
        => Wallet.For(ctx.Target).Gold >= minGold;
}
```

内置：**`HasCompletedTaskCondition`**——当宿主已完成某 data-task 至少 _N_ 次时通过。

```csharp
var cond = new HasCompletedTaskCondition(killNpc, "King", quantity: 1);
bool ok = cond.IsMet(host.MakeContext());
```

**事件**——继承 `NarrativeEvent`，覆写 `Execute`。事件在某个 `Runtime` 阶段执行
（`Start` / `End` / `Both`），可带自己的前置 `Conditions`，还有 `RefireOnLoad`（读档是否重放——
像“给 500 XP”这类一次性发放应设 `false`）。

内置：**`CompleteDataTaskEvent`**——运行时在宿主上完成一个 data-task。这就是对话行或任务节点把进度
推进系统的方式：

```csharp
var node = new DialogueNode_NPC("greet", new DialogueLine("欢迎回来。"));
node.AddEvent(new CompleteDataTaskEvent(talkTo, "Elder")); // 到达时完成 "talkto_elder"
```

`NarrativeNodeBase.ProcessEvents(context, phase)` 会按阶段过滤、检查每个事件自己的条件、再执行——
对话控制器会替你调它。

## 对话

对话是一张**按 ID 互引的扁平节点图**（`DialogueGraph`），由 `DialogueController` 按 *chunk* 推进，
由你的 `IDialoguePresenter` 画出。一个 chunk = 一串连续的 NPC 台词 + 该串末尾的合法玩家选项。

### 用代码搭一张图

```csharp
var graph = new DialogueGraph { RootId = "npc_hello" };

var hello = new DialogueNode_NPC("npc_hello", new DialogueLine("你好，旅人。"), speakerId: "elder");
hello.AddPlayerReply("ask_quest");
hello.AddPlayerReply("leave");

var askQuest = new DialogueNode_Player("ask_quest", new DialogueLine("有活儿给我吗？"));
askQuest.AddNpcReply("npc_quest");

var leave = new DialogueNode_Player("leave", new DialogueLine("再见。"));

var npcQuest = new DialogueNode_NPC("npc_quest", new DialogueLine("去清理那些狼。"), "elder");
npcQuest.AddEvent(new CompleteDataTaskEvent(talkTo, "Elder")); // 这行播放时触发

graph.AddNode(hello);
graph.AddNode(askQuest);
graph.AddNode(leave);
graph.AddNode(npcQuest);
```

节点之间用**字符串 ID**互引，而非对象引用（这是“扁平运行时模板”设计——由图按 ID 解析）。
没有文本的 `DialogueNode_Player` 是**路由节点**，会被自动选中；节点也可以显式设 `AutoSelect`。
每个节点可持有一条主 `Line` 加若干受条件门控的 `AlternativeLines`，`GetRandomLine` 从符合条件者里随机选。

也可以把图包进 **`DialogueAsset`**（**Assets → Create → Sigil → Narrative → Dialogue**）当项目资产来编辑/存储。

### 跑一段对话

控制器与表现分离：它决定*谁说什么、有哪些选项*；你的呈现器决定*怎么显示、这一行何时算播完*，
播完时回调 `AdvanceLine()`。

```csharp
public sealed class ConsolePresenter : IDialoguePresenter
{
    private DialogueController _c;

    public void OnDialogueBegan(DialogueController c) => _c = c; // controller 在这里交给你
    public void OnLineStarted(DialogueNode node, DialogueLine line)
    {
        Debug.Log(line.Text);
        _c.AdvanceLine();                 // 这里立即推进；真实 UI 会等这一行的时长
    }
    public void OnLineFinished(DialogueNode node, DialogueLine line) { }
    public void OnResponsesAvailable(IReadOnlyList<DialogueNode_Player> options)
    {
        _c.SelectOption(options[0]);      // 真实 UI 会显示按钮、等玩家点
    }
    public void OnDialogueEnded(DialogueController c) { }
}

var controller = new DialogueController(graph, host.MakeContext(), new ConsolePresenter());
controller.Begin();
```

你会用到的控制器接口：`Begin()`、`AdvanceLine()`、`SelectOption(option)`，以及只读的
`Phase`（`Idle` / `NpcLine` / `AwaitingChoice` / `PlayerLine` / `Ended`）、`CurrentNode`、
`CurrentLine`、`AvailableResponses`。

> 💡 **行时长**由呈现器说了算。`DialogueLine.Duration`（一个 `ELineDuration`）和
> `GetReadingTime(lettersPerSecond, minDisplayTime)` 只给你*意图*；何时调 `AdvanceLine()` 由你定。
> 核心从不自己跑计时。

## 任务

一个任务就是一台**状态机**。`QuestState` 持有分支；`QuestBranch` 持有任务和一个目标状态；
当某分支的任务全部完成，任务就**取**该分支、进入其目标状态。到达 `Success` 或 `Failure` 状态则结束。

### 编写一个任务资产

用 **Assets → Create → Sigil → Narrative → Quest** 创建。一个任务由“起始状态 ID + 一组状态”构成。
状态/分支/任务都是 `[SerializeReference]`，所以自定义 `QuestTask` 子类会出现在 Inspector 下拉里
（暂无专门的图编辑器）。同样结构用代码写：

```csharp
// start --b1[ 完成 "talkto_elder" x2 ]--> done(Success)
var start = new QuestState("start");
var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
branch.AddTask(new CompleteDataTaskQuestTask(talkTo, "Elder", requiredQuantity: 2));
start.AddBranch(branch);
var done = new QuestState("done", EStateNodeType.Success);
```

想在代码里构建资产本身（程序化任务、样例、测试）而非 Inspector 编辑，用
`QuestAsset.Create(questId, startStateId, states)`——数据任务则用 `DataTaskDefinition.Create(taskName)`。

**任务项**继承自 `QuestTask`：进度累计到 `RequiredQuantity`，可设为 `Optional`（门控里恒视为完成）、
`Hidden`，并提供 `GetDescription()` / `GetProgressText()`（如 `"1/2"`）。内置的
**`CompleteDataTaskQuestTask`** 在宿主每次完成匹配的 data-task 时 +1——于是对话/世界事件通过同一条
data-task 通道驱动任务。要写自己的，继承 `QuestTask` 并调 `AddProgress` / `Complete`。

### 通过宿主运行任务

宿主持有任务列表，**以资产为键**（每个 `QuestAsset` 同时只有一个活实例，对齐原引擎按 quest class 为键）。
`BeginQuest` 会把资产**深克隆**成一个干净的运行实例，因此模板保持纯净，每次运行都从零进度开始。

```csharp
Quest q = host.BeginQuest(questAsset);        // 克隆 + 开始；若已在跑则返回 null
host.IsQuestInProgress(questAsset);           // true
host.CompleteDataTask(talkTo, "Elder");       // 1/2
host.CompleteDataTask(talkTo, "Elder");       // 2/2 → 取分支 → Success
host.IsQuestSucceeded(questAsset);            // true

host.RestartQuest(questAsset);                // 从头重玩（全新实例）
host.ForgetQuest(questAsset);                 // 丢弃；之后可再次 BeginQuest
```

查询：`GetQuestInstance`、`IsQuestInProgress` / `IsQuestSucceeded` / `IsQuestFailed` /
`IsQuestFinished` / `IsQuestStartedOrFinished`，以及列表 `GetInProgressQuests()` /
`GetSucceededQuests()` / `GetFailedQuests()` / `AllQuests`。

### 响应任务事件

每个运行中任务的事件都会**桥接到宿主级事件**，于是你的 UI 在一个地方订阅，不管是哪个任务触发：

```csharp
host.QuestStarted            += quest => ShowTracker(quest);
host.QuestNewState           += (quest, state) => RefreshObjectives(quest);
host.QuestTaskProgressChanged += (quest, branch, task, oldP, newP) => UpdateBar(task, newP);
host.QuestTaskCompleted      += (quest, branch, task) => Tick(task);
host.QuestBranchCompleted    += (quest, branch) => { };
host.QuestSucceeded          += quest => Celebrate(quest);
host.QuestFailed             += quest => ShowFailure(quest);
host.QuestForgotten          += quest => HideTracker(quest);
host.QuestRestarted          += oldInstance => { }; // 携带被替换掉的旧实例
```

> 📌 `ForgetQuest` / `RestartQuest` 会调 `Quest.Deinitialize()`，结束当前状态的任务、让它们从宿主解绑——
> 被遗忘任务的任务项**不会**继续消费 data-task。永远通过宿主丢弃任务，别自己攥着引用。

### 状态事件（进入状态时的副作用）

`QuestState` 是 `NarrativeNodeBase`，因此带一个 `Events` 列表。它的事件在任务**进入**该状态时触发（阶段 `Start`）、
**离开**时触发（阶段 `End`）——包括 `Success` / `Failure` 终止态，所以“成功即发奖励”不过是挂在成功态上的一个事件。
像写任何[事件](#条件与事件节点模型)一样写、再挂上去：

```csharp
var done = new QuestState("done", EStateNodeType.Success);
done.AddEvent(new GrantRewardEvent(gold: 100));   // 任务到达 "done" 时触发
```

这里 **`RefireOnLoad`** 很关键：读档会把任务重建到存档所处状态，从而重入该状态、重跑其进入事件。
所以把一次性效果（发金币、+XP——这些已经存在你自己的存档里）标成 `RefireOnLoad = false`，读档时就**不会**重复应用；
需要重新应用的效果（UI 横幅、重新注册运行时状态）保持默认的 `RefireOnLoad = true`。见 **Quest state events & RefireOnLoad** 样例。

> 注：`QuestState` 也继承了 `Conditions`，但本版本进入状态**不做**条件门控——状态上只消费 `Events`。

### 轮询任务与 `NarrativeRunner`

多数任务是事件驱动的——data-task 一触发任务就推进，无需每帧干活。对于必须**轮询**的任务
（“待在某区域 3 秒”“等到夜晚”），把任务的 `TickInterval` 设 > 0 并覆写 `Tick()`。这些 tick 得有人驱动：
在 `NarrativeComponent` 旁边挂一个 **`NarrativeRunner`** 组件，它每帧替你调 `host.TickActiveTasks(Time.deltaTime)`
（你也可以在自己的循环里手动调 `TickActiveTasks`）。

```csharp
public sealed class StayInZoneTask : QuestTask   // TickInterval 比如 0.25 秒
{
    protected override void OnBeginTask() { /* “进入时是否已在区域内”的一次性检查放这 */ }
    public override void Tick()
    {
        if (PlayerIsInZone(Context.Target)) AddProgress(1); else /* 重置 */ ;
    }
}
```

tick 会累积真实时间、大帧时补齐，且某次 tick 中途完成的任务不会破坏其他任务。**begin 后不会自动 tick 一次**
——“进入即检查”的逻辑请放 `OnBeginTask`。

## 存档与读档

叙事存档只覆盖**叙事状态**——任务进度 + 主任务表。（对话是瞬态、不存；通用世界对象存档是后续里程碑。）
两个宿主方法对应原作的 `PrepareForSave` / `PerformLoad`：

```csharp
// 快照当前状态（每个任务的所处状态 + 各分支任务进度 + 到达过的状态，加主任务表）到一个 JSON 友好的 DTO。
NarrativeSaveData data = host.CaptureNarrativeState();

// ……之后在一个全新宿主上——把每个任务重建到存档所处状态并回填进度。
// knownQuests 是你那些“可能出现在存档里的 QuestAsset”目录（按 QuestId 匹配）。
host.RestoreNarrativeState(data, allMyQuestAssets);
```

读档是**静默的**：重建任务时不触发任何 `OnQuest*` 宿主事件（读档期间 `host.IsLoading` 为 `true`），
于是你的 UI 不会在读档时收到一堆假的“任务开始”事件。还原出来的任务是**活的**——其任务项会重新订阅宿主，
之后正常游戏照常推进。

> 📌 每个 `QuestAsset` 需要一个稳定的 **`QuestId`**（在资产上设置；留空则回退用资产名）。它是存档键——
> **一旦玩家有存档就别改它**，否则那些任务在读档时会认不回来。

用 `NarrativeSaveManager` 把 DTO 序列化成 JSON 并落盘。文件读写走可注入的 `IFileSystem`（默认
`DiskFileSystem`），于是测试可以传一个内存假实现：

```csharp
var manager = new NarrativeSaveManager();                  // 或 new NarrativeSaveManager(myFileSystem)
string path = System.IO.Path.Combine(Application.persistentDataPath, "narrative.json");

manager.Save(path, host.CaptureNarrativeState());          // 写 JSON
NarrativeSaveData loaded = manager.Load(path);             // 读回（不存在则 null）
if (loaded != null) host.RestoreNarrativeState(loaded, allMyQuestAssets);

// 或者只要 JSON 字符串、不碰文件：
string json = NarrativeSaveManager.ToJson(data);
NarrativeSaveData back = NarrativeSaveManager.FromJson(json);
```

> 💡 读档时任务会重入存档所处状态，于是该状态的进入事件会重跑——除了标了 `RefireOnLoad = false` 的一次性事件。
> 见[状态事件](#状态事件进入状态时的副作用)。

## `NarrativeComponent` 速查表

```csharp
// ---- 数据任务 ----
bool  CompleteDataTask(DataTaskDefinition task, string argument, int quantity = 1);
bool  CompleteDataTask(string taskName, string argument, int quantity = 1);
MasterTaskList MasterTasks { get; }
event Action<string> DataTaskCompleted;         // 规范化原始串

// ---- 上下文 ----
NarrativeContext MakeContext(GameObject target = null);

// ---- 任务：驱动 ----
Quest BeginQuest(QuestAsset quest, string startFromId = null);
bool  RestartQuest(QuestAsset quest, string startFromId = null);
bool  ForgetQuest(QuestAsset quest);

// ---- 任务：查询 ----
Quest GetQuestInstance(QuestAsset quest);
bool  IsQuestInProgress(QuestAsset quest);
bool  IsQuestSucceeded(QuestAsset quest);
bool  IsQuestFailed(QuestAsset quest);
bool  IsQuestFinished(QuestAsset quest);
bool  IsQuestStartedOrFinished(QuestAsset quest);
List<Quest> GetInProgressQuests();
List<Quest> GetSucceededQuests();
List<Quest> GetFailedQuests();
IReadOnlyList<Quest> AllQuests { get; }

// ---- 任务：响应（宿主级，桥接自每个运行中的任务）----
event Action<Quest> QuestStarted, QuestForgotten, QuestRestarted, QuestSucceeded, QuestFailed;
event Action<Quest, QuestState> QuestNewState;
event Action<Quest, QuestBranch> QuestBranchCompleted;
event Action<Quest, QuestBranch, QuestTask, int, int> QuestTaskProgressChanged;
event Action<Quest, QuestBranch, QuestTask> QuestTaskCompleted;

// ---- 存档（叙事状态）----
NarrativeSaveData CaptureNarrativeState();
bool RestoreNarrativeState(NarrativeSaveData data, IEnumerable<QuestAsset> knownQuests);
bool IsLoading { get; }

// ---- tick（轮询任务；通常由 NarrativeRunner 组件驱动）----
void TickActiveTasks(float deltaSeconds);
```

本版本对话**不**走宿主——直接构造 `DialogueController`（见[对话](#对话)）。宿主侧的对话编排在计划中。

## 从 Narrative Pro / UE 迁移过来？

这是对 Narrative Pro / Narrative Arsenal（Narrative Tools 出品）**设计**的从零 C# 重写。大致对应：

| Narrative Pro (UE) | 本包 | 说明 |
|---|---|---|
| `UTalesComponent` | `NarrativeComponent` | 组件而非基类；加到任意 GameObject。 |
| `UNarrativeDataTask` | `DataTaskDefinition` | `ScriptableObject`；`MakeTaskString` 规范化一致。 |
| `MasterTaskList`（TMap） | `MasterTaskList` | 同样的 原始串 → 次数 记录。 |
| `TSubclassOf<UQuest>`（任务身份） | `QuestAsset`（任务身份） | 宿主按资产为键管理任务列表。 |
| `UQuest` / `UQuestState` / `UQuestBranch` / `UNarrativeTask` | `Quest` / `QuestState` / `QuestBranch` / `QuestTask` | 相同的状态机语义。 |
| 每次 `BeginQuest` 都 `NewObject<UQuest>` | `QuestAsset.CreateRuntimeQuest`（深克隆） | 每次运行都是新实例；模板不动。 |
| `UDialogue`（编译后） | `DialogueGraph` + `DialogueController` | 扁平 ID 互引图；无蓝图编译步骤。 |
| 对话表现代码 | `IDialoguePresenter` | 表现从核心里彻底剥出。 |
| `UNarrativeCondition` / `UNarrativeEvent` | `NarrativeCondition` / `NarrativeEvent` | `[SerializeReference]` 多态。 |
| 复制 / GAS / 组队 / 图编辑器 | *（超出范围）* | 仅单机——见下。 |

## 尚未包含的部分

- **对话/任务的图编辑器**——目前通过 Inspector 编辑扁平模板（大图会比较吃力）。
- **通用世界对象存档**（GUID 键、任意组件）。已发布的存档层只覆盖叙事状态（任务 + data-task）；
  泛型 `ISaveable` / `SaveableEntity` 路径是后续里程碑。
- **表现层**（过场、相机、Avatar）、**战斗/GAS 集成**、**AI**、**联机/复制**、**角色创建器**——
  刻意超出范围，与 Sigil GAS 核心相同的取舍。

---

以 [MIT](../LICENSE.md) 授权。叙事**设计**沿用 Narrative Tools 的 *Narrative Pro / Narrative Arsenal*，
在 Unity C# 中从零重写；不含任何第三方引擎或源码。
