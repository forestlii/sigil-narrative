[English](Usage.md) | [简体中文](Usage.zh-CN.md)

# Sigil Narrative — Usage

A practical guide to using the narrative framework: **data-tasks**, the **condition/event** node
model, **dialogue**, and the **quest** state machine, all driven through one host component.

> **Status: `0.x`, work in progress.** An in-editor graph editor is not shipped yet (see
> [What's not here yet](#whats-not-here-yet)). The public API is unstable and may change.

## Contents

1. [Install](#install)
2. [Core concepts at a glance](#core-concepts-at-a-glance)
3. [5-minute quick start](#5-minute-quick-start)
4. [Data tasks & the master task list](#data-tasks--the-master-task-list)
5. [Conditions & events (the node model)](#conditions--events-the-node-model)
6. [Dialogue](#dialogue)
7. [Quests](#quests)
8. [Saving & loading](#saving--loading)
9. [`NarrativeComponent` cheat sheet](#narrativecomponent-cheat-sheet)
10. [Coming from Narrative Pro / UE?](#coming-from-narrative-pro--ue)
11. [What's not here yet](#whats-not-here-yet)

---

## Install

Copy the `com.likeon.narrative` folder into your project's `Packages/` directory, or use
**Package Manager → Add package from disk…** and pick `package.json`. It is standalone — there are no
other dependencies to install.

To run the bundled EditMode tests when the package lives outside your project folder, add it to
`"testables"` in `Packages/manifest.json`, then open **Window → General → Test Runner**:

```json
"testables": [ "com.likeon.narrative" ]
```

## Core concepts at a glance

| Concept | Type | What it is |
|---|---|---|
| **Data task** | `DataTaskDefinition` (ScriptableObject) | A lightweight `Name_Argument` marker — "the player did X". Has no logic; it is normalized to a raw string. |
| **Master task list** | `MasterTaskList` | The persistent record: raw task string → how many times completed. The coupling point between dialogue and quests. |
| **Host** | `NarrativeComponent` (`MonoBehaviour`, implements `INarrativeHost`) | The single entry point: complete data-tasks, begin/query quests, build a context. No base class to inherit. |
| **Context** | `NarrativeContext` | What conditions/events receive: `Host` + an optional `Target` GameObject. |
| **Condition / Event** | `NarrativeCondition` / `NarrativeEvent` | Reusable, serializable logic attached to nodes — gate content, fire side effects. |
| **Dialogue** | `DialogueGraph` + `DialogueController` + `IDialoguePresenter` | A flat, ID-linked node graph; the controller advances it; your presenter draws it. |
| **Quest** | `QuestAsset` → `Quest` (State / Branch / Task) | A state machine: tasks in a branch complete → take the branch → enter the next state. |

The mental model in one line: **data-tasks record what happened → conditions read them and events
write them → dialogue and quests are just graphs of nodes that carry conditions/events.**

## 5-minute quick start

```csharp
using Likeon.Narrative;
using UnityEngine;

public class Demo : MonoBehaviour
{
    [SerializeField] private DataTaskDefinition talkTo; // a Data Task asset, e.g. TaskName = "TalkTo"

    private void Start()
    {
        // 1. The host — add it to any GameObject (no base class required).
        var host = gameObject.AddComponent<NarrativeComponent>();

        // 2. Listen for completions (raw normalized task string, e.g. "talkto_elder").
        host.DataTaskCompleted += raw => Debug.Log($"completed: {raw}");

        // 3. Record that the player talked to the Elder.
        host.CompleteDataTask(talkTo, "Elder");

        // 4. Query it back later.
        bool met = host.MasterTasks.HasCompleted(talkTo, "Elder");   // true
        int times = host.MasterTasks.GetCount(talkTo, "Elder");      // 1
    }
}
```

Create the `DataTaskDefinition` asset via **Assets → Create → Sigil → Narrative → Data Task**, set
its **Task Name** (e.g. `TalkTo`), and drag it into the field. That is the whole loop dialogue and
quests build on.

## Data tasks & the master task list

A **data task** is just a normalized string. `DataTaskDefinition.MakeTaskString(argument)` lowercases
`TaskName_Argument` and strips spaces — `"Talk To" + "King Bob"` → `"talkto_kingbob"`. This matches
the original engine's behavior exactly, including keeping the underscore when the argument is empty
(`"kill_"`).

You rarely build the string yourself. Complete tasks through the host, which writes to the
`MasterTaskList` and broadcasts:

```csharp
host.CompleteDataTask(task, "Elder");          // via a DataTaskDefinition
host.CompleteDataTask("TalkTo", "Elder");      // or by name, no asset needed
host.CompleteDataTask(task, "Wolf", quantity: 3);
```

Query the record directly:

```csharp
MasterTaskList tasks = host.MasterTasks;
tasks.GetCount(task, "Wolf");        // 3
tasks.HasCompleted(task, "Wolf", 5); // false — only 3 so far
```

> 💡 The master task list is the **save-core primitive** and the bridge between systems: a dialogue
> option can require `HasCompleted(KillNPC, King)`, and a quest task can advance when
> `CompleteDataTask(TalkTo, Elder)` fires. You will see both below.

## Conditions & events (the node model)

Dialogue nodes and quest states share one base, `NarrativeNodeBase`: an `Id`, a list of
**conditions** (all must pass for the node to be usable), and a list of **events** (fired when the
node is reached). Both conditions and events are `[SerializeReference]` polymorphic — subclass them
to add your own, and they show up in the Inspector's managed-reference dropdown.

**Conditions** — subclass `NarrativeCondition`, override `CheckCondition`. `Not` inverts the result;
callers use `IsMet` (which applies `Not`).

```csharp
[System.Serializable]
public sealed class HasGoldCondition : NarrativeCondition
{
    [SerializeField] private int minGold;
    public override bool CheckCondition(NarrativeContext ctx)
        => Wallet.For(ctx.Target).Gold >= minGold;
}
```

Built-in: **`HasCompletedTaskCondition`** — passes when the host has completed a data-task at least
_N_ times.

```csharp
var cond = new HasCompletedTaskCondition(killNpc, "King", quantity: 1);
bool ok = cond.IsMet(host.MakeContext());
```

Built-in: **`QuestStateCondition`** — passes when a quest is in a chosen state (`InProgress` /
`Succeeded` / `Failed` / `Finished` / `StartedOrFinished`). A never-started quest is `false` for
every query. Combine with `Not` for "unless".

```csharp
// e.g. only offer this dialogue option after the intro quest succeeded
var cond = new QuestStateCondition(introQuest, EQuestStateQuery.Succeeded);
bool ok = cond.IsMet(host.MakeContext());
```

**Events** — subclass `NarrativeEvent`, override `Execute`. An event runs at a `Runtime` phase
(`Start` / `End` / `Both`), can carry its own gating `Conditions`, and has `RefireOnLoad` (whether a
save-load should replay it — set `false` for one-shot grants like "give 500 XP").

Built-in: **`CompleteDataTaskEvent`** — completes a data-task on the host when it runs. This is how a
dialogue line or quest node pushes progress into the system:

```csharp
var node = new DialogueNode_NPC("greet", new DialogueLine("Welcome back."));
node.AddEvent(new CompleteDataTaskEvent(talkTo, "Elder")); // completing "talkto_elder" on reach
```

Built-in: **`BeginQuestEvent`** — starts a quest on the host (the canonical "talk to NPC → begin
quest"). It defaults to `RefireOnLoad = false` because starting a quest is a one-shot side effect.

```csharp
var line = new DialogueNode_NPC("giveQuest", new DialogueLine("Bring me the gemstone."));
line.AddEvent(new BeginQuestEvent(gemstoneQuest)); // begins the quest when this line plays
```

`NarrativeNodeBase.ProcessEvents(context, phase)` filters by phase, checks each event's own
conditions, then executes — the dialogue controller calls this for you.

## Dialogue

Dialogue is a **flat graph of ID-linked nodes** (`DialogueGraph`), advanced in *chunks* by a
`DialogueController`, and drawn by your `IDialoguePresenter`. A chunk = a chain of consecutive NPC
lines followed by the valid player options at the end of that chain.

### Authoring a graph in code

```csharp
var graph = new DialogueGraph { RootId = "npc_hello" };

var hello = new DialogueNode_NPC("npc_hello", new DialogueLine("Hello, traveler."), speakerId: "elder");
hello.AddPlayerReply("ask_quest");
hello.AddPlayerReply("leave");

var askQuest = new DialogueNode_Player("ask_quest", new DialogueLine("Any work for me?"));
askQuest.AddNpcReply("npc_quest");

var leave = new DialogueNode_Player("leave", new DialogueLine("Goodbye."));

var npcQuest = new DialogueNode_NPC("npc_quest", new DialogueLine("Clear the wolves."), "elder");
npcQuest.AddEvent(new CompleteDataTaskEvent(talkTo, "Elder")); // fires when this line plays

graph.AddNode(hello);
graph.AddNode(askQuest);
graph.AddNode(leave);
graph.AddNode(npcQuest);
```

Nodes reference each other **by string ID**, not object reference (this is the "flat runtime
template" design — the graph resolves IDs). A `DialogueNode_Player` with no text is a **routing
node** and is auto-selected; a node can also set `AutoSelect` explicitly. Each node can hold a main
`Line` plus condition-gated `AlternativeLines`, and `GetRandomLine` picks among the eligible ones.

You can also wrap a graph in a **`DialogueAsset`** (**Assets → Create → Sigil → Narrative →
Dialogue**) to author/store it as a project asset.

### Running a dialogue

The controller is presentation-agnostic: it decides *who says what and what the options are*; your
presenter decides *how to show it and when the line is done*, then calls back `AdvanceLine()`.

```csharp
public sealed class ConsolePresenter : IDialoguePresenter
{
    private DialogueController _c;

    public void OnDialogueBegan(DialogueController c) => _c = c; // controller is handed to you here
    public void OnLineStarted(DialogueNode node, DialogueLine line)
    {
        Debug.Log(line.Text);
        _c.AdvanceLine();                 // instant here; a real UI waits for the line's duration
    }
    public void OnLineFinished(DialogueNode node, DialogueLine line) { }
    public void OnResponsesAvailable(IReadOnlyList<DialogueNode_Player> options)
    {
        _c.SelectOption(options[0]);      // a real UI shows buttons and waits for input
    }
    public void OnDialogueEnded(DialogueController c) { }
}

var controller = new DialogueController(graph, host.MakeContext(), new ConsolePresenter());
controller.Begin();
```

Controller surface you'll use: `Begin()`, `AdvanceLine()`, `SelectOption(option)`, and read-only
`Phase` (`Idle` / `NpcLine` / `AwaitingChoice` / `PlayerLine` / `Ended`), `CurrentNode`,
`CurrentLine`, `AvailableResponses`.

> 💡 **Line duration** is the presenter's call. `DialogueLine.Duration` (an `ELineDuration`) and
> `GetReadingTime(lettersPerSecond, minDisplayTime)` give you the *intent*; you decide when to call
> `AdvanceLine()`. The core never runs a clock.

## Quests

A quest is a **state machine**. A `QuestState` holds branches; a `QuestBranch` holds tasks and a
destination state; when a branch's tasks are all complete, the quest **takes** that branch and enters
its destination. Reaching a `Success` or `Failure` state ends the quest.

### Authoring a quest asset

Create via **Assets → Create → Sigil → Narrative → Quest**. A quest is authored as a start-state id
plus a set of states. States/branches/tasks are `[SerializeReference]`, so custom `QuestTask`
subclasses appear in the Inspector's dropdown (there is no dedicated graph editor yet). The same
shape in code:

```csharp
// start --b1[ complete "talkto_elder" x2 ]--> done(Success)
var start = new QuestState("start");
var branch = new QuestBranch { Id = "b1", DestinationStateId = "done" };
branch.AddTask(new CompleteDataTaskQuestTask(talkTo, "Elder", requiredQuantity: 2));
start.AddBranch(branch);
var done = new QuestState("done", EStateNodeType.Success);
```

To build the asset object itself in code (procedural quests, samples, tests) instead of in the
Inspector, use `QuestAsset.Create(questId, startStateId, states)` — and `DataTaskDefinition.Create(taskName)`
for data tasks.

**Tasks** derive from `QuestTask`: they track progress up to `RequiredQuantity`, can be `Optional`
(always counts as complete for gating), `Hidden`, and expose `GetDescription()` / `GetProgressText()`
(e.g. `"1/2"`). The built-in **`CompleteDataTaskQuestTask`** advances by +1 each time a matching
data-task completes on the host — so dialogue/world events drive quests through the same data-task
channel. Write your own by subclassing `QuestTask` and calling `AddProgress` / `Complete`.

### Running quests through the host

The host owns the quest list, keyed **by asset** (one live instance per `QuestAsset`, mirroring how
the original engine keys by quest class). `BeginQuest` **deep-clones** the asset into a fresh runtime
instance, so the template stays pristine and every run starts at zero progress.

```csharp
Quest q = host.BeginQuest(questAsset);        // clone + start; null if already running
host.IsQuestInProgress(questAsset);           // true
host.CompleteDataTask(talkTo, "Elder");       // 1/2
host.CompleteDataTask(talkTo, "Elder");       // 2/2 → branch taken → Success
host.IsQuestSucceeded(questAsset);            // true

host.RestartQuest(questAsset);                // replay from scratch (fresh instance)
host.ForgetQuest(questAsset);                 // drop it; can BeginQuest again later
```

Queries: `GetQuestInstance`, `IsQuestInProgress` / `IsQuestSucceeded` / `IsQuestFailed` /
`IsQuestFinished` / `IsQuestStartedOrFinished`, and the lists `GetInProgressQuests()` /
`GetSucceededQuests()` / `GetFailedQuests()` / `AllQuests`.

### Reacting to quest events

Every running quest's events are **bridged to host-level events**, so your UI subscribes in one place
regardless of which quest fires:

```csharp
host.QuestStarted            += quest => ShowTracker(quest);
host.QuestNewState           += (quest, state) => RefreshObjectives(quest);
host.QuestTaskProgressChanged += (quest, branch, task, oldP, newP) => UpdateBar(task, newP);
host.QuestTaskCompleted      += (quest, branch, task) => Tick(task);
host.QuestBranchCompleted    += (quest, branch) => { };
host.QuestSucceeded          += quest => Celebrate(quest);
host.QuestFailed             += quest => ShowFailure(quest);
host.QuestForgotten          += quest => HideTracker(quest);
host.QuestRestarted          += oldInstance => { }; // carries the replaced instance
```

> 📌 `ForgetQuest` / `RestartQuest` call `Quest.Deinitialize()`, which ends the current state's tasks
> so they unsubscribe from the host — a forgotten quest's tasks will **not** keep consuming
> data-tasks. Always drop quests through the host, not by holding your own reference.

### State events (side effects on entering a state)

A `QuestState` is a `NarrativeNodeBase`, so it carries an `Events` list. Its events fire when the
quest **enters** the state (phase `Start`) and **leaves** it (phase `End`) — including terminal
`Success` / `Failure` states, so "on success, grant a reward" is just an event on the success state.
Write one like any [event](#conditions--events-the-node-model) and attach it:

```csharp
var done = new QuestState("done", EStateNodeType.Success);
done.AddEvent(new GrantRewardEvent(gold: 100));   // fires when the quest reaches "done"
```

**`RefireOnLoad`** matters here: loading a save rebuilds the quest at its saved state, which re-enters
that state and re-runs its entry events. So mark one-shot effects (grant gold, +XP — things already
persisted in your own save) with `RefireOnLoad = false` so they are **not** re-applied on load; leave
`RefireOnLoad = true` (the default) for effects that must be re-applied (UI banners, re-registering
runtime state). See the **Quest state events & RefireOnLoad** sample.

> Note: `QuestState` also inherits `Conditions`, but state entry is **not** condition-gated in this
> version — only `Events` are consumed on states.

A **`QuestBranch`** is also a `NarrativeNodeBase`, so branches carry `Events` too. A branch's events
fire when it **activates** (phase `Start` — when its owning state is entered) and **deactivates**
(phase `End` — when the branch is taken, or a sibling is taken and the state is left). Same
`RefireOnLoad` filtering applies on load. Taking a branch fires its End events **exactly once**
(deactivation is idempotent). Like states, branch `Conditions` are inherited but dormant — branch
selection stays task-driven.

```csharp
var branch = new QuestBranch { Id = "toGemstone", DestinationStateId = "hasGem" };
branch.AddTask(new CompleteDataTaskQuestTask(pickUp, "Gemstone", 1));
branch.AddEvent(new GrantRewardEvent(gold: 50) { Runtime = EEventRuntime.End }); // when this branch is taken
```

### Polling tasks & `NarrativeRunner`

Most tasks are event-driven — a data-task fires and the task advances, no per-frame work. For tasks
that must *poll* ("stay in a zone for 3 seconds", "wait until night"), set `TickInterval > 0` and
override `Tick()`. Something has to drive those ticks: add a **`NarrativeRunner`** component next to
your `NarrativeComponent` and it calls `host.TickActiveTasks(Time.deltaTime)` each frame for you (or
call `TickActiveTasks` yourself from your own loop).

```csharp
public sealed class StayInZoneTask : QuestTask   // TickInterval e.g. 0.25s
{
    protected override void OnBeginTask() { /* one-shot "already in zone?" check goes here */ }
    public override void Tick()
    {
        if (PlayerIsInZone(Context.Target)) AddProgress(1); else /* reset */ ;
    }
}
```

Ticks accumulate real time and catch up on long frames, and a task that completes mid-tick can't
corrupt the others. The first tick is **not** fired automatically on begin — put "check on enter"
logic in `OnBeginTask`.

## Saving & loading

The narrative save covers **narrative state only** — quest progress and the master task list.
(Dialogue is transient and not saved; general world-object saving is a later milestone.) Two host
methods mirror the original's `PrepareForSave` / `PerformLoad`:

```csharp
// Capture a snapshot (per-quest current state + branch task progress + reached states, plus the
// master task list) into a plain, JSON-friendly DTO.
NarrativeSaveData data = host.CaptureNarrativeState();

// ... later, on a fresh host — rebuild every quest at its saved state and refill progress.
// `knownQuests` is your catalog of QuestAssets that could appear in a save (matched by QuestId).
host.RestoreNarrativeState(data, allMyQuestAssets);
```

Restore is **silent**: it rebuilds quests without firing any `OnQuest*` host events (`host.IsLoading`
is `true` for the duration), so your UI won't see a burst of fake "quest started" events on load.
Restored quests are **live** — their tasks re-subscribe to the host, so gameplay continues normally.

> 📌 Each `QuestAsset` needs a stable **`QuestId`** (set it on the asset; it falls back to the asset
> name if left blank). It is the save key — **don't change it once players have saves**, or those
> quests won't be recognized on load.

Serialize the DTO to JSON and to disk with `NarrativeSaveManager`. File I/O goes through an
injectable `IFileSystem` (default `DiskFileSystem`), so tests can pass an in-memory fake:

```csharp
var manager = new NarrativeSaveManager();                  // or new NarrativeSaveManager(myFileSystem)
string path = System.IO.Path.Combine(Application.persistentDataPath, "narrative.json");

manager.Save(path, host.CaptureNarrativeState());          // write JSON
NarrativeSaveData loaded = manager.Load(path);             // read back (null if missing)
if (loaded != null) host.RestoreNarrativeState(loaded, allMyQuestAssets);

// Or just the JSON string, no files:
string json = NarrativeSaveManager.ToJson(data);
NarrativeSaveData back = NarrativeSaveManager.FromJson(json);
```

> 💡 On load, the quest re-enters its saved state, so that state's entry events re-run — except those
> marked `RefireOnLoad = false` (one-shot grants). See [State events](#state-events-side-effects-on-entering-a-state).

## `NarrativeComponent` cheat sheet

```csharp
// ---- Data tasks ----
bool  CompleteDataTask(DataTaskDefinition task, string argument, int quantity = 1);
bool  CompleteDataTask(string taskName, string argument, int quantity = 1);
MasterTaskList MasterTasks { get; }
event Action<string> DataTaskCompleted;         // raw normalized string

// ---- Context ----
NarrativeContext MakeContext(GameObject target = null);

// ---- Quests: drive ----
Quest BeginQuest(QuestAsset quest, string startFromId = null);
bool  RestartQuest(QuestAsset quest, string startFromId = null);
bool  ForgetQuest(QuestAsset quest);

// ---- Quests: query ----
Quest GetQuestInstance(QuestAsset quest);
bool  IsQuestInProgress(QuestAsset quest);
bool  IsQuestSucceeded(QuestAsset quest);
bool  IsQuestFailed(QuestAsset quest);
bool  IsQuestFinished(QuestAsset quest);
bool  IsQuestStartedOrFinished(QuestAsset quest);
List<Quest> GetInProgressQuests();
List<Quest> GetSucceededQuests();
List<Quest> GetFailedQuests();
IReadOnlyList<Quest> AllQuests { get; }

// ---- Quests: react (host-level, bridged from every running quest) ----
event Action<Quest> QuestStarted, QuestForgotten, QuestRestarted, QuestSucceeded, QuestFailed;
event Action<Quest, QuestState> QuestNewState;
event Action<Quest, QuestBranch> QuestBranchCompleted;
event Action<Quest, QuestBranch, QuestTask, int, int> QuestTaskProgressChanged;
event Action<Quest, QuestBranch, QuestTask> QuestTaskCompleted;

// ---- Save (narrative state) ----
NarrativeSaveData CaptureNarrativeState();
bool RestoreNarrativeState(NarrativeSaveData data, IEnumerable<QuestAsset> knownQuests);
bool IsLoading { get; }

// ---- Ticking (polling tasks; usually driven by the NarrativeRunner component) ----
void TickActiveTasks(float deltaSeconds);
```

Dialogue is **not** run through the host in this version — construct a `DialogueController` directly
(see [Dialogue](#dialogue)). Host-side dialogue orchestration is planned.

## Coming from Narrative Pro / UE?

This is a from-scratch C# reimplementation of the **design** of Narrative Pro / Narrative Arsenal
(by Narrative Tools). Rough mapping:

| Narrative Pro (UE) | This package | Note |
|---|---|---|
| `UTalesComponent` | `NarrativeComponent` | Component, not a base class; add to any GameObject. |
| `UNarrativeDataTask` | `DataTaskDefinition` | `ScriptableObject`; same `MakeTaskString` normalization. |
| `MasterTaskList` (TMap) | `MasterTaskList` | Same raw-string → count record. |
| `TSubclassOf<UQuest>` (quest identity) | `QuestAsset` (quest identity) | Host keys the quest list by asset. |
| `UQuest` / `UQuestState` / `UQuestBranch` / `UNarrativeTask` | `Quest` / `QuestState` / `QuestBranch` / `QuestTask` | Same state-machine semantics. |
| `NewObject<UQuest>` per `BeginQuest` | `QuestAsset.CreateRuntimeQuest` (deep clone) | Fresh per-run instance; template untouched. |
| `UDialogue` (compiled) | `DialogueGraph` + `DialogueController` | Flat ID-linked graph; no blueprint compile step. |
| Dialogue presentation code | `IDialoguePresenter` | Presentation pulled out of the core entirely. |
| `UNarrativeCondition` / `UNarrativeEvent` | `NarrativeCondition` / `NarrativeEvent` | `[SerializeReference]` polymorphic. |
| Replication / GAS / party / editor graphs | *(out of scope)* | Single-player only — see below. |

## What's not here yet

- **In-editor graph editors** for dialogue and quests — author flat templates via the Inspector for
  now (rough for large graphs).
- **General world-object saving** (GUID-keyed, arbitrary components). The shipped save layer covers
  narrative state only (quests + data-tasks); a generic `ISaveable` / `SaveableEntity` path is a
  later milestone.
- **Presentation layer** (cutscenes, camera, avatars), **combat/GAS integration**, **AI**,
  **networking/replication**, **character creator** — intentionally out of scope, mirroring the same
  cuts as the Sigil GAS core.

---

Licensed under [MIT](../LICENSE.md). The narrative **design** follows *Narrative Pro / Narrative
Arsenal* by Narrative Tools, reimplemented from scratch in Unity C#; no third-party engine or source
code is included.
