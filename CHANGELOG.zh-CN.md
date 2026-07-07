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
