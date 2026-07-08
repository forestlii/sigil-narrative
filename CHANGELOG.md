[English](CHANGELOG.md) | [简体中文](CHANGELOG.zh-CN.md)

# Changelog

All notable changes to this package are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
While the public API is unstable it stays in the `0.y.z` range and may break without a major bump.

## [Unreleased]

### Added
- Package scaffolding: `com.likeon.narrative` (standalone, no dependencies),
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
- `Dialogue` runtime: `DialogueController` (chunk advance — NPC reply chain, condition-filtered
  player options, auto-select, next-chunk generation) and the presentation-agnostic
  `IDialoguePresenter` interface; `DialogueAsset` (ScriptableObject wrapper for a `DialogueGraph`).
- `Quest` system: the `Quest` state machine (`QuestState` / `QuestBranch` / `QuestTask`,
  `EnterState`/`TakeBranch`, Success/Failure terminal states) with the full event set
  (Started / NewState / TaskProgressChanged / TaskCompleted / BranchCompleted / Succeeded / Failed);
  abstract `QuestTask` (progress, `Optional` — always-complete for gating, `Hidden`, tick hook);
  and built-in `CompleteDataTaskQuestTask` (advances when a matching data-task completes).
- `Quest` asset & host integration: `QuestAsset` (ScriptableObject template holding a start-state id
  and a state set; `CreateRuntimeQuest` deep-clones states/branches/tasks so the template stays pristine
  and each run gets fresh progress). `NarrativeComponent` now manages a quest list keyed by asset —
  `BeginQuest` / `RestartQuest` / `ForgetQuest`, queries (`GetQuestInstance`,
  `IsQuestInProgress` / `IsQuestSucceeded` / `IsQuestFailed` / `IsQuestFinished` /
  `IsQuestStartedOrFinished`, `GetInProgressQuests` / `GetSucceededQuests` / `GetFailedQuests`,
  `AllQuests`) — and bridges each running quest's events to host-level
  `QuestStarted` / `QuestForgotten` / `QuestRestarted` / `QuestNewState` / `QuestBranchCompleted` /
  `QuestTaskProgressChanged` / `QuestTaskCompleted` / `QuestSucceeded` / `QuestFailed`.
  `Quest.Deinitialize` ends active tasks (unsubscribing host hooks) on forget/restart.
- `Save` (narrative state, single-player path only): `NarrativeSaveData` DTO (JSON via `JsonUtility`)
  capturing quest progress (current state, per-branch task progress, reached states) plus the master
  task list; `QuestAsset.QuestId` as the stable save key. `NarrativeComponent.CaptureNarrativeState` /
  `RestoreNarrativeState(data, knownQuests)` mirror UE `PrepareForSave` / `PerformLoad` — restore
  rebuilds each quest at its saved state, refills task progress via the non-broadcasting
  `RestoreProgress`, and stays silent (`IsLoading`, no `OnQuest*` events fire). `NarrativeSaveManager`
  does JSON + file I/O behind an injectable `IFileSystem` (default `DiskFileSystem`).
- `Integration`: `NarrativeComponent.TickActiveTasks(deltaSeconds)` drives polling tasks
  (`QuestTask.TickInterval > 0`) via `QuestTask.DriveTick` (time accumulator; catches up on large
  frames; snapshot + `IsActive` guard so a task completing mid-tick can't corrupt iteration), and the
  thin `NarrativeRunner` MonoBehaviour feeds it `Time.deltaTime` each frame. Code-authoring factories
  `QuestAsset.Create` and `DataTaskDefinition.Create` for procedural quests / samples / tests.
  End-to-end EditMode coverage (dialogue event → data-task → quest → save → restore) and an importable
  **End-to-end demo** sample (`Samples~/EndToEndDemo`).
- `Quest` state events: `QuestState` now extends `NarrativeNodeBase`, so its `Events` fire on entering
  (phase `Start`) and leaving (phase `End`) a state — including terminal `Success` / `Failure` states
  (e.g. grant a reward on success). This activates `RefireOnLoad`: on load a quest re-enters its saved
  state, and `ProcessEvents(..., isLoading: true)` skips one-shot events (`RefireOnLoad == false`) so
  rewards aren't re-granted, while `RefireOnLoad == true` events re-run. New **Quest state events &
  RefireOnLoad** sample (`Samples~/QuestStateEvents`).
- Docs: bilingual README, MIT `LICENSE`, and a bilingual usage guide (`Documentation~/Usage.md`).
