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
- 文档：双语 README、MIT `LICENSE`、以及 `Documentation~/` 下的双语 devlog。
