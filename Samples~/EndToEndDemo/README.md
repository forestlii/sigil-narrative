# End-to-end demo

A single script that walks the whole loop in one Play session:

**begin quest → play dialogue → a dialogue event completes a data-task → the quest advances to
success → save to disk → restore on a fresh host.**

## Run it

1. Import this sample via **Package Manager → Sigil - Narrative → Samples → Import**.
2. Create an empty scene, add an empty GameObject, and attach **`NarrativeEndToEndDemo`**
   (Add Component → Sigil → Narrative → Samples → End-To-End Demo).
3. Press **Play** and watch the **Console** print each step.

Everything is built in code (`DataTaskDefinition.Create`, `QuestAsset.Create`, a `DialogueGraph`)
so there are no assets to author — you can just press Play. In a real project you would author the
quest / data-task / dialogue as assets in the Inspector and reference them from your own scripts
instead of constructing them at runtime.

## What to look for in the Console

```
=== Narrative end-to-end demo ===
[quest] started: DemoQuest
[quest] new state: start
[quest] in progress? True
[dialogue] playing…
[dialogue] npc_hello: "Well met. Consider it done."
[dialogue] ended
[quest] new state: done
[quest] SUCCEEDED: DemoQuest
[quest] succeeded after dialogue? True
[data-task] talkto_elder count = 1
[save] wrote <persistentDataPath>/narrative_demo.json
[load] restored — quest succeeded? True, talkto_elder = 1
=== demo complete ===
```

## Polling tasks (the `NarrativeRunner`)

This demo is fully event-driven, so it does not need per-frame ticking. If you write a **polling**
task (a `QuestTask` with `TickInterval > 0`, e.g. "stay in a zone for 3 seconds"), add a
**`NarrativeRunner`** component next to your `NarrativeComponent` — it feeds `Time.deltaTime` to the
quest system each frame so those tasks get ticked.

See [`Documentation~/Usage.md`](../../Documentation~/Usage.md) for the full guide.
