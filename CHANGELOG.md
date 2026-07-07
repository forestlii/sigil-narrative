[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

# Changelog

All notable changes to this package are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the public API is unstable it stays in the `0.y.z` range and may break without a major bump.

## [Unreleased]

### Added
- Package scaffolding: `com.likeon.narrative` (depends on `com.likeon.gas` for the GameplayTag system),
  `Likeon.Narrative.Runtime` / `.Editor` / `.Tests.EditMode` assembly definitions.
- `Core`: `DataTaskDefinition` (lightweight `Name_Argument` task marker) and `MasterTaskList`
  (persistent record of completed data-tasks; the coupling point between quests and dialogue).
- `Core` framework: `NarrativeNodeBase` (id + conditions + events), abstract `NarrativeCondition`
  (with `Not`) and `NarrativeEvent` (with `EEventRuntime` Start/End/Both, `RefireOnLoad`, and
  event-level conditions), `NarrativeContext`, and the `INarrativeHost` contract.
- Host: `NarrativeComponent` (MonoBehaviour implementing `INarrativeHost`) — no forced base class;
  records/queries data-tasks and broadcasts completion.
- Built-ins: `HasCompletedTaskCondition` and `CompleteDataTaskEvent`.
- `Dialogue` data model: `DialogueLine` (+ `ELineDuration`, reading-time and Default-fallback
  resolution, condition-gated selection), `DialogueNode` / `DialogueNode_NPC` / `DialogueNode_Player`
  (routing detection, auto-select, condition-filtered line selection), and `DialogueGraph`
  (flat node list with by-ID lookup).
- Docs: bilingual README, MIT `LICENSE`, and a bilingual devlog under `Documentation~/`.
