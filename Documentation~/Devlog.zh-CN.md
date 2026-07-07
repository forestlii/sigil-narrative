[English](Devlog.md) | [简体中文](Devlog.zh-CN.md)

# 开发日志 — Sigil Narrative

一份持续更新的开发日志：做了什么，以及设计**为什么**这么走。最新的在最上面。

---

## 2026-07-08 — Core 框架 + 对话引擎（M1–M2）

两个里程碑落地，各自在 Unity 里（批处理 `-runTests`，6000.4.10f1）实跑验证通过。

**M1 — Core 底座。** 对话与任务共用的条件/事件/节点骨架：抽象 `NarrativeCondition`（带 `Not` 取反）
与 `NarrativeEvent`（带 `Start`/`End`/`Both` 时机、`RefireOnLoad` 存档标志、事件级条件）、按条件门控
并按阶段触发事件的 `NarrativeNodeBase`、替代原作 `(Pawn, Controller, TalesComponent)` 三元组的
`NarrativeContext` / `INarrativeHost`、以及 `NarrativeComponent` 宿主（MonoBehaviour——不强制继承基类）。
两个内置把节点接到 data-task 层：`HasCompletedTaskCondition` 与 `CompleteDataTaskEvent`。按角色
目标过滤与 party 策略按范围砍掉（单机、无角色系统）。

**M2 — 对话。** 分支对话引擎。数据模型：`DialogueLine`（阅读时长算法 + `Default` 时长回退解析、条件
门控选行）、NPC/玩家节点（路由判定、自动选择、备选行过滤），以及 `DialogueGraph`——按 string ID 寻址
的扁平节点表，正对应“跳过图编辑器、直接编辑扁平模板”的决策。运行时：`DialogueController` 以 *chunk*
推进对话（一条 NPC 回复链，接链尾节点的条件过滤玩家选项），自动选中路由/自动选项并从所选回复生成下一
chunk。关键在于控制器与表现层解耦——它通过 `IDialoguePresenter` 回调，把“这一行何时播完”交给宿主决定；
原作那一大堆过场/相机/音频代码全留在外面。测试里的 `RecordingPresenter` 断言了一次完整分支走查的精确
回调序列。

下一步：任务（State/Branch/Task 状态机），然后存档，然后端到端 demo。

---

## 2026-07-07 — 范围、架构、第一个里程碑

**这是什么。** *Narrative Pro / Narrative Arsenal*（Narrative Tools）**叙事内核**的 Unity C# 实现：
对话、任务、存档。

**范围——不做 1:1 移植。** 源码是一套完整的联机战斗 RPG 框架：8 个模块、约 530 个文件，深度绑定
GameplayAbilitySystem、联机/网络同步、AI、角色创建器、以及 Slate 图编辑器。全套移植是数个人月，且约
40% 卡在“重写而非翻译”的硬骨头（GAS、联机）。本项目刻意把范围收到**叙事内核**：**对话 + 任务 + 存档**。
战斗、联机、AI、角色创建器、编辑器内图编辑器全部排除。

**关键架构发现。** 原作里，对话/任务数据走的是“*图编辑器 → 蓝图编译 → 扁平运行时模板*”这条链，
运行时只吃编译后的扁平模板。所以本版**彻底跳过图编辑器与编译层**，直接把那个扁平模板定义成
Unity 资产（一个 `ScriptableObject` 存扁平节点表、节点间用 string ID 引用）。这是能拿到的最大简化。

**生态对齐。** 早期骨架一度走了“纯 .NET + 自造 GameplayTag”的路——这是错的，因为本包属于 Sigil
生态、应复用其成熟的 GameplayTag 系统（[`com.likeon.gas`](https://github.com/forestlii/sigil-gas)）。
于是整个作废、重新对齐：包 `com.likeon.narrative`、命名空间 `Likeon.Narrative`、复用 Sigil 的
GameplayTag、用 Unity Test Framework 验证——与 Sigil 其余部分保持一致。

**设计立场。** 不强制继承基类：宿主是 `MonoBehaviour` 组件、可存档对象实现 `ISaveable` 接口——
你永远不必去继承一个“NarrativeActor”。核心与表现层解耦（对话暴露 presenter 接口，过场是宿主的活）。

**第一片已交付。** Core 数据任务层：
- `DataTaskDefinition` —— `名_参数` 标记，规范化方式与原作一致（转小写、删空格；对照源码坐实，不是猜的）。
- `MasterTaskList` —— “玩家这辈子做过什么”的持久记录；任务与对话的耦合点，也是存档核心原语。
- 6 个 EditMode 测试，在 Unity 6000.4.10f1 上实跑全绿。
