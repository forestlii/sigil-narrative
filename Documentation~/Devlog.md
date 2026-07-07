[English](Devlog.md) | [简体中文](Devlog.zh-CN.md)

# Devlog — Sigil Narrative

A running development log: what got built, and *why* the design went the way it did.
Newest entry first.

---

## 2026-07-08 — Core framework + dialogue engine (M1–M2)

Two milestones landed, each verified green in Unity (batch-mode `-runTests`, 6000.4.10f1).

**M1 — Core foundation.** The condition/event/node backbone that both dialogue and quests build on:
an abstract `NarrativeCondition` (with a `Not` flip) and `NarrativeEvent` (with `Start`/`End`/`Both`
runtime phases, a `RefireOnLoad` save flag, and event-level conditions), a `NarrativeNodeBase` that
gates on conditions and fires events by phase, a `NarrativeContext` / `INarrativeHost` pair replacing
the original's `(Pawn, Controller, TalesComponent)` triple, and a `NarrativeComponent` host (a
MonoBehaviour — no forced base class). Two built-ins wire nodes to the data-task layer:
`HasCompletedTaskCondition` and `CompleteDataTaskEvent`. Per-character target filters and party
policies were dropped (single-player scope, no character system).

**M2 — Dialogue.** The branching dialogue engine. Data model: `DialogueLine` (reading-time math +
the `Default`-duration fallback resolution, condition-gated selection), NPC/Player nodes (routing
detection, auto-select, alternative-line filtering), and a `DialogueGraph` — a flat node table
addressed by string IDs, matching the "skip the graph editor, edit the flattened template directly"
decision. Runtime: `DialogueController` advances the conversation in *chunks* (an NPC reply chain,
then the last node's condition-filtered player options), auto-selecting routing/auto options and
generating the next chunk from the chosen reply. Crucially the controller is presentation-agnostic —
it emits callbacks through `IDialoguePresenter` and lets the host decide when a line is "done"; the
original's cinematics/camera/audio code stays out. A `RecordingPresenter` in the tests asserts the
exact callback sequence of a full branching walk.

Next: quests (State/Branch/Task state machine), then save, then an end-to-end demo.

---

## 2026-07-07 — Scope, architecture, first milestone

**What this is.** A Unity C# implementation of the **narrative core** of *Narrative Pro /
Narrative Arsenal* (Narrative Tools): dialogue, quests and save.

**Scope — not a 1:1 port.** The source is a full multiplayer combat RPG framework: 8 modules,
~530 files, a deep GameplayAbilitySystem, networking/replication, AI, a character creator, and Slate
graph editors. Porting all of it would be several person-months, and ~40% sits in "rewrite, not
translate" territory (GAS, replication). The scope here is deliberately narrowed to the
**narrative core**: **dialogue + quest + save**. Combat, networking, AI, the character creator, and
the in-editor graph editors are out.

**The key architectural finding.** In the original, dialogue/quest data flows through a
*graph editor → blueprint compile → flattened runtime template* pipeline; the runtime only ever
consumes the flattened template. So this version **skips the graph editor and compile layer entirely**
and defines that flattened template directly as Unity assets (a `ScriptableObject` holding a flat node
list referenced by string IDs). Biggest simplification available.

**Ecosystem alignment.** An early scaffold went down a "pure-.NET, hand-rolled GameplayTag" path —
the wrong call, since this package belongs to the Sigil ecosystem and should reuse its mature
GameplayTag system ([`com.likeon.gas`](https://github.com/forestlii/sigil-gas)). It was scrapped and
re-aligned: package `com.likeon.narrative`, namespace `Likeon.Narrative`, reuse the Sigil GameplayTag
system, and verify with the Unity Test Framework — matching the rest of Sigil.

**Design stance.** No forced base class: hosting is a `MonoBehaviour` component and saveable objects
implement an `ISaveable` interface — you never inherit a "NarrativeActor". The core stays
presentation-agnostic (dialogue exposes a presenter interface; cinematics are the host's job).

**First slice shipped.** The Core data-task layer:
- `DataTaskDefinition` — the `Name_Argument` marker, normalized like the original (lowercase, spaces
  stripped; verified against the source, not guessed).
- `MasterTaskList` — the persistent "what has the player ever done" record; the coupling point
  between quests and dialogue and a save-core primitive.
- 6 EditMode tests, run green on Unity 6000.4.10f1.
