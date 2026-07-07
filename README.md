# Sigil — Narrative (Dialogue · Quest · Save) for Unity

[English](README.md) | [简体中文](README.zh-CN.md)

A single-player **narrative framework** for Unity: branching **dialogue**, a state-machine
**quest** system, and **save/load**. Part of the Sigil ecosystem — it reuses the GameplayTag
system from [`com.likeon.gas`](https://github.com/forestlii/sigil-gas) rather than shipping its own.

- **Engine:** Unity 6 — developed & verified on 6000.4.10f1
- **Scope:** single-player logic (no networking, no combat — see roadmap)
- **Publisher:** Likeon · namespace `Likeon.Narrative`
- **Depends on:** [`com.likeon.gas`](https://github.com/forestlii/sigil-gas) (GameplayTag system)
- **Devlog:** the development story & design decisions live in [`Documentation~/Devlog.md`](Documentation~/Devlog.md)

> This reimplements the **design** of *Narrative Pro / Narrative Arsenal* by Narrative Tools —
> the dialogue graph, quest state machine and data-task model — as an independent C# rewrite for
> Unity, from scratch. No third-party engine or
> source code is included. See [Attribution](#attribution).

> **Status: early / work-in-progress (`0.x`).** The package is being built milestone by milestone.
> What exists today is the package skeleton and the **Core data-task layer**; dialogue, quest and
> save are on the roadmap below. The public API is unstable and may break without a major bump.

## Install

Copy the `com.likeon.narrative` folder into your project's `Packages/` directory, or use
**Package Manager → Add package from disk…** and select `package.json`. You also need
[`com.likeon.gas`](https://github.com/forestlii/sigil-gas) installed the same way (it is the only
dependency).

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
- **Reuse, don't reinvent.** GameplayTags come from `com.likeon.gas`.
- **Presentation-agnostic.** Dialogue exposes a presenter interface; cinematics/camera/audio are the
  host project's job, not baked into the core.
- **Testable logic.** State machines and normalization are authored to run under EditMode tests.

## What's here today

- **`DataTaskDefinition`** — a lightweight `Name_Argument` task marker (a `ScriptableObject`),
  normalized the same way as the original (`lowercased, spaces stripped`).
- **`MasterTaskList`** — the persistent record of completed data-tasks (the coupling point between
  quests and dialogue, and a save-core primitive).
- EditMode test coverage for the above (green on 6000.4.10f1).

## Roadmap

| Milestone | Status |
|---|---|
| Package scaffold + Core data-task layer | ✅ done |
| Core: node base, conditions, events, context | ⏳ next |
| Dialogue: graph, chunk-based runner, presenter interface | ⬜ planned |
| Quest: State / Branch / Task state machine | ⬜ planned |
| Save: JSON DTO save/load for narrative state | ⬜ planned |
| Integration: `NarrativeComponent` host + end-to-end demo | ⬜ planned |

Intentionally **out of scope** (mirrors the same cuts as the Sigil GAS core): combat, networking/
replication, AI, character creator, and in-editor graph editors.

## Attribution

The narrative model (branching dialogue graph, quest state machine, data-tasks) follows the
**design** of *Narrative Pro / Narrative Arsenal* by **Narrative Tools**, reimplemented from scratch
in Unity C#. No third-party engine or source code is
included.

## License

[MIT](LICENSE.md) — free for any use including commercial, just keep the copyright notice.
