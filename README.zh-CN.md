# Sigil — 叙事（对话 · 任务 · 存档）for Unity

[English](README.md) | [简体中文](README.zh-CN.md)

面向 Unity 单机游戏的**叙事框架**：分支**对话**、状态机**任务**系统、**存档/读档**。
属于 Sigil 生态——复用 [`com.likeon.gas`](https://github.com/forestlii/sigil-gas) 的 GameplayTag 系统，
不自带一套。

- **引擎：** Unity 6 —— 在 6000.4.10f1 上开发与验证
- **范围：** 单机逻辑（不做联机、不做战斗，见路线图）
- **发布者：** Likeon · 命名空间 `Likeon.Narrative`
- **依赖：** [`com.likeon.gas`](https://github.com/forestlii/sigil-gas)（GameplayTag 系统）
- **使用指南：** 完整用法见 [`Documentation~/Usage.zh-CN.md`](Documentation~/Usage.zh-CN.md)（中 / 英）

> 本项目把 Narrative Tools 的 *Narrative Pro / Narrative Arsenal* 的**设计**（对话图、任务状态机、
> data-task 模型）以独立 C# 重写到 Unity——从零实现，不含任何第三方引擎或源码。
> 见[致谢](#致谢)。

> **状态：开发中（`0.x`）。** 本包按里程碑逐步搭建。目前已交付：**Core 数据任务层**、**条件/事件节点模型**、
> **对话**系统、**任务**状态机、以及**叙事状态存档/读档**（均有 EditMode 测试覆盖）。图编辑器与端到端 demo 是下一步——见路线图。
> 公开 API 未稳定，可能不经大版本号变更即破坏兼容。

## 安装

把 `com.likeon.narrative` 文件夹拷进你工程的 `Packages/` 目录，或用
**Package Manager → Add package from disk…** 选择 `package.json`。你还需要同样方式装上
[`com.likeon.gas`](https://github.com/forestlii/sigil-gas)（它是唯一依赖）。

### 跑测试

本包在 `Tests/` 下带有 EditMode 测试。由于包被从工程文件夹外引用，需要把它加进工程
`Packages/manifest.json` 的 `"testables"`，再打开 **Window → General → Test Runner**：

```json
"testables": [ "com.likeon.narrative" ]
```

## 设计原则

- **不强制继承基类。** 宿主是 `MonoBehaviour` 组件；可存档对象实现接口（`ISaveable`）——
  你永远不必去继承一个"NarrativeActor"。
- **复用，不重造。** GameplayTag 直接用 `com.likeon.gas` 的。
- **与表现层解耦。** 对话暴露一个 presenter 接口；过场/相机/音频是宿主工程的活，不焊死在核心里。
- **逻辑可测。** 状态机与规范化逻辑按 EditMode 可测来写。

## 目前已有

- **数据任务** —— `DataTaskDefinition`（`名_参数` 的 `ScriptableObject`，规范化与原作一致）与
  `MasterTaskList`（已完成任务的持久记录；对话与任务的耦合点）。
- **节点模型** —— `NarrativeNodeBase` + `[SerializeReference]` 的 `NarrativeCondition` /
  `NarrativeEvent`，含内置（`HasCompletedTaskCondition`、`CompleteDataTaskEvent`）。
- **对话** —— 扁平、按 ID 互引的 `DialogueGraph`，分块推进的 `DialogueController`，
  以及与表现解耦的 `IDialoguePresenter`（可用 `DialogueAsset` 存成资产）。
- **任务** —— `Quest` 状态机（`QuestState` / `QuestBranch` / `QuestTask`）、每次运行克隆的
  `QuestAsset` 模板，以及 `NarrativeComponent` 上的宿主侧管理 / 查询 / 事件。
- **存档** —— 叙事状态存档/读档：JSON DTO（`NarrativeSaveData`）、宿主上的 `CaptureNarrativeState` /
  `RestoreNarrativeState`，以及带可注入 `IFileSystem` 的 `NarrativeSaveManager`。
- **宿主** —— `NarrativeComponent`（`MonoBehaviour`，无需继承基类）。
- 以上全部的 EditMode 测试覆盖（在 6000.4.10f1 上全绿）。

完整用法见 **[`Documentation~/Usage.zh-CN.md`](Documentation~/Usage.zh-CN.md)**。

## 路线图

| 里程碑 | 状态 |
|---|---|
| 包骨架 + Core 数据任务层 | ✅ 完成 |
| Core：节点基类、条件、事件、上下文 | ✅ 完成 |
| 对话：图、分块运行器、presenter 接口 | ✅ 完成 |
| 任务：State / Branch / Task 状态机 + 宿主集成 | ✅ 完成 |
| 存档：叙事状态的 JSON DTO 存/读 | ✅ 完成 |
| 集成：端到端 demo | ⏳ 下一步 |

刻意**排除在范围外**（与 Sigil GAS 核心同样的取舍）：战斗、联机/网络同步、AI、角色创建器、
编辑器内的图编辑器。

## 致谢

叙事模型（分支对话图、任务状态机、data-task）借鉴 **Narrative Tools** 的
*Narrative Pro / Narrative Arsenal* 的**设计**，以 Unity C# 从零重写。
不含任何第三方引擎或源码。

## 许可

[MIT](LICENSE.md) —— 可自由用于任何用途（含商业），保留版权声明即可。
