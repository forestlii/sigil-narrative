# Quest state events & `RefireOnLoad`

A quest **state** can carry `NarrativeEvent`s that fire when the quest reaches it — grant a reward,
pop a banner, start another dialogue, etc. This sample shows how to write your own state event and
what **`RefireOnLoad`** does when a save is loaded.

## Run it

1. Import via **Package Manager → Sigil - Narrative → Samples → Import**.
2. New empty scene → empty GameObject → add **`QuestStateEventsDemo`**
   (Add Component → Sigil → Narrative → Samples → Quest State Events Demo).
3. Press **Play** and read the **Console**.

## The idea

A `QuestState` is a `NarrativeNodeBase`, so it has an `Events` list. On **entering** a state its
events run with phase `Start`; on **leaving** it they run with phase `End`. Terminal
`Success` / `Failure` states fire their entry events too — so "on success, grant a reward" is just an
event on the success state.

Writing one is the same as any `NarrativeEvent`:

```csharp
[System.Serializable]
public sealed class GrantGoldEvent : NarrativeEvent
{
    [SerializeField] private int amount = 100;
    public GrantGoldEvent(int amount) { this.amount = amount; RefireOnLoad = false; }
    public override void Execute(NarrativeContext context) => Wallet.Add(amount);
}

// ...attach it to a state:
done.AddEvent(new GrantGoldEvent(100));
```

## `RefireOnLoad` — the one thing to get right

When you **load a save**, the quest is rebuilt at its saved state, which re-enters that state and
re-runs its entry events. That is a problem for one-shot effects: you don't want to grant the reward
again. So each event has `RefireOnLoad`:

| `RefireOnLoad` | Re-runs on load? | Use for |
|---|---|---|
| `false` | no | one-shot effects already persisted elsewhere — grant gold/items, unlock, +XP |
| `true` (default) | yes | effects that must be re-applied — UI banners, re-registering runtime state |

The demo puts **both** on the success state and prints the wallet before and after a save/load:

```
=== Quest state events demo ===
[quest] started; triggering the objective…
[event] banner: Quest complete!
[event] granted 100 gold (wallet now 100)
[quest] succeeded — wallet = 100
[save] wrote <persistentDataPath>/narrative_state_events_demo.json
--- simulating restart + load ---
[event] banner: Quest complete!            ← RefireOnLoad = true  → re-fired
[load] restored — quest succeeded? True; wallet = 100   ← gold NOT re-granted (RefireOnLoad = false)
=== demo complete ===
```

See [`Documentation~/Usage.md`](../../Documentation~/Usage.md) for the full guide.
