[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

# 更新日志

本包的重要变更记录于此。
格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。
公开 API 未稳定期间停留在 `0.y.z`，可能不经大版本号变更即破坏兼容。

## [未发布]

### 新增
- 包骨架：`com.likeon.narrative`（依赖 `com.likeon.gas` 复用其 GameplayTag 系统），
  `Likeon.Narrative.Runtime` / `.Editor` / `.Tests.EditMode` 程序集定义。
- `Core`：`DataTaskDefinition`（轻量 `名_参数` 任务标记）与 `MasterTaskList`
  （已完成 data-task 的持久记录；任务与对话的耦合点）。
- `Core` 框架：`NarrativeNodeBase`（ID + 条件 + 事件）、抽象 `NarrativeCondition`（带 `Not`）与
  `NarrativeEvent`（带 `EEventRuntime` Start/End/Both、`RefireOnLoad`、事件级条件）、
  `NarrativeContext`、以及 `INarrativeHost` 契约。
- 宿主：`NarrativeComponent`（实现 `INarrativeHost` 的 MonoBehaviour）——不强制继承基类；
  记录/查询 data-task 并广播完成。
- 内置：`HasCompletedTaskCondition` 与 `CompleteDataTaskEvent`。
- `Dialogue` 数据模型：`DialogueLine`（+ `ELineDuration`、阅读时长与 Default 回退解析、条件门控选行）、
  `DialogueNode` / `DialogueNode_NPC` / `DialogueNode_Player`（路由判定、自动选择、条件过滤选行）、
  以及 `DialogueGraph`（扁平节点表 + by-ID 查找）。
- `Dialogue` 运行时：`DialogueController`（chunk 推进——NPC 回复链、条件过滤玩家选项、自动选择、
  生成下一 chunk）与表现层无关的 `IDialoguePresenter` 接口；`DialogueAsset`（包 `DialogueGraph` 的 ScriptableObject）。
- `Quest` 系统：`Quest` 状态机（`QuestState` / `QuestBranch` / `QuestTask`、`EnterState`/`TakeBranch`、
  Success/Failure 终止态）+ 全套事件（Started / NewState / TaskProgressChanged / TaskCompleted /
  BranchCompleted / Succeeded / Failed）；抽象 `QuestTask`（进度、`Optional`——门控里恒视为完成、
  `Hidden`、tick 钩子）；内置 `CompleteDataTaskQuestTask`（匹配 data-task 完成时推进）。
- `Quest` 资产与宿主集成：`QuestAsset`（ScriptableObject 模板，持起始状态 ID + 状态集；
  `CreateRuntimeQuest` 深克隆状态/分支/任务，模板保持纯净、每次运行都是全新进度）。
  `NarrativeComponent` 现以资产为身份键管理任务列表——`BeginQuest` / `RestartQuest` / `ForgetQuest`、
  查询（`GetQuestInstance`、`IsQuestInProgress` / `IsQuestSucceeded` / `IsQuestFailed` / `IsQuestFinished` /
  `IsQuestStartedOrFinished`、`GetInProgressQuests` / `GetSucceededQuests` / `GetFailedQuests`、`AllQuests`）——
  并把每个运行中任务的事件桥接到宿主级
  `QuestStarted` / `QuestForgotten` / `QuestRestarted` / `QuestNewState` / `QuestBranchCompleted` /
  `QuestTaskProgressChanged` / `QuestTaskCompleted` / `QuestSucceeded` / `QuestFailed`。
  遗忘/重启时 `Quest.Deinitialize` 结束进行中的任务（解绑宿主订阅）。
- `Save`（叙事状态，仅单机路径一）：`NarrativeSaveData` DTO（经 `JsonUtility` 走 JSON），保存任务进度
  （所处状态、各分支任务进度、到达过的状态）加主任务表；`QuestAsset.QuestId` 作稳定存档键。
  `NarrativeComponent.CaptureNarrativeState` / `RestoreNarrativeState(data, knownQuests)` 对应
  UE `PrepareForSave` / `PerformLoad`——读档把每个任务重建到存档所处状态、用不广播的 `RestoreProgress`
  回填进度，全程静默（`IsLoading`，不触发任何 `OnQuest*` 事件）。`NarrativeSaveManager` 负责 JSON + 文件读写，
  文件层走可注入的 `IFileSystem`（默认 `DiskFileSystem`）。注：`NarrativeEvent.RefireOnLoad` 暂无消费者
  ——本移植的任务状态不携带事件。
- `Integration`：`NarrativeComponent.TickActiveTasks(deltaSeconds)` 通过 `QuestTask.DriveTick`
  驱动轮询任务（`QuestTask.TickInterval > 0`）——时间累积、大帧补 tick、快照 + `IsActive` 守卫
  （某次 tick 中途完成任务也不会破坏迭代）；薄壳 `NarrativeRunner` MonoBehaviour 每帧喂 `Time.deltaTime`。
  代码构建工厂 `QuestAsset.Create` 与 `DataTaskDefinition.Create`（程序化任务 / 样例 / 测试用）。
  端到端 EditMode 覆盖（对话事件 → data-task → 任务 → 存档 → 读档）+ 可导入的**端到端样例**
  （`Samples~/EndToEndDemo`）。
- 文档：双语 README、MIT `LICENSE`、以及双语使用指南（`Documentation~/Usage.md`）。
