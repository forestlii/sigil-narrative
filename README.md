# Sigil — Narrative (Dialogue · Quest · Save) for Unity

[English](README.md) | [简体中文](README.zh-CN.md)

A single-player **narrative framework** for Unity: branching **dialogue**, a state-machine
**quest** system, and **save/load**. Standalone and string-based — **no third-party or ecosystem
dependencies**.

- **Engine:** Unity 6 — developed & verified on 6000.4.10f1
- **Scope:** single-player logic (no networking, no combat — see roadmap)
- **Publisher:** Likeon · namespace `Likeon.Narrative`
- **Dependencies:** none
- **Usage:** full guide in [`Documentation~/Usage.md`](Documentation~/Usage.md) (English / 简体中文)

> **Status: work-in-progress (`0.x`).** Built milestone by milestone. Shipped today: the **Core
> data-task layer**, the **condition/event node model**, the **dialogue** system, the **quest**
> state machine, **narrative-state save/load**, and **runtime integration** (task ticking + an
> end-to-end demo sample) — all with EditMode coverage. The core narrative loop is complete; an
> in-editor graph editor is the main remaining nice-to-have. The public API is unstable and may break
> without a major bump.

## Install

Copy the `com.likeon.narrative` folder into your project's `Packages/` directory, or use
**Package Manager → Add package from disk…** and select `package.json`. There are no other
dependencies to install.

### Running tests

The package ships with EditMode tests under `Tests/`. Because it is referenced from outside the
project folder, add it to `"testables"` in your project's `Packages/manifest.json`, then open
**Window → General → Test Runner**:

```json
"testables": [ "com.likeon.narrative" ]
```

## Design principles

- **No forced base class.** Hosting is a `MonoBehaviour` component; saveable objects implement an
  interface (`ISaveable`) — you never inherit a "NarrativeActor".
- **Standalone & string-based.** Tasks, ids and quest keys are plain strings — no dependency on a tag
  system or any other package.
- **Presentation-agnostic.** Dialogue exposes a presenter interface; cinematics/camera/audio are the
  host project's job, not baked into the core.
- **Testable logic.** State machines and normalization are authored to run under EditMode tests.

## What's here today

- **Data tasks** — `DataTaskDefinition` (a `Name_Argument` `ScriptableObject`, normalized like the
  original) and `MasterTaskList` (the persistent completed-task record; the coupling point between
  dialogue and quests).
- **Node model** — `NarrativeNodeBase` with `[SerializeReference]` `NarrativeCondition` /
  `NarrativeEvent`, plus built-ins (`HasCompletedTaskCondition`, `CompleteDataTaskEvent`).
- **Dialogue** — a flat ID-linked `DialogueGraph`, a chunk-based `DialogueController`, and the
  presentation-agnostic `IDialoguePresenter` (`DialogueAsset` to store one as an asset).
- **Quests** — the `Quest` state machine (`QuestState` / `QuestBranch` / `QuestTask`), `QuestAsset`
  templates cloned per run, state entry/exit events (`RefireOnLoad`-aware), and host-side management /
  queries / events on `NarrativeComponent`.
- **Save** — narrative-state save/load: a JSON DTO (`NarrativeSaveData`), `CaptureNarrativeState` /
  `RestoreNarrativeState` on the host, and `NarrativeSaveManager` with an injectable `IFileSystem`.
- **Integration** — task ticking for polling tasks (`NarrativeComponent.TickActiveTasks` + the
  `NarrativeRunner` component), code-authoring factories (`QuestAsset.Create`,
  `DataTaskDefinition.Create`), and an importable **End-to-end demo** sample.
- **Host** — `NarrativeComponent` (a `MonoBehaviour`, no base class to inherit).
- EditMode test coverage across all of the above, including an end-to-end pass (green on 6000.4.10f1).

See **[`Documentation~/Usage.md`](Documentation~/Usage.md)** for the full guide.

## Roadmap

| Milestone | Status |
|---|---|
| Package scaffold + Core data-task layer | ✅ done |
| Core: node base, conditions, events, context | ✅ done |
| Dialogue: graph, chunk-based runner, presenter interface | ✅ done |
| Quest: State / Branch / Task state machine + host integration | ✅ done |
| Save: JSON DTO save/load for narrative state | ✅ done |
| Integration: task ticking + end-to-end demo | ✅ done |

Intentionally **out of scope** (mirrors the same cuts as the Sigil GAS core): combat, networking/
replication, AI, character creator, and in-editor graph editors.

## License

[MIT](LICENSE.md) — free for any use including commercial, just keep the copyright notice.
