# Enemy Behavior API

**This is a library for mod developers. On its own it does nothing you can see.**
Install it because another mod lists it as a dependency — or because you are writing
that mod.

It reads any enemy's PlayMaker FSMs by reflection, works out which states are attacks
and which are movement, and hands that to other mods as a structured schema they can
observe, tune, or drive.

There is no per-enemy list anywhere in it. Point it at a GameObject and it tells you
what that enemy can do — an enemy it has never seen works exactly like one it has, so
mods built on it do not need per-enemy support added for each new creature.

## For players

Install it and nothing changes. No hotkeys, no menus, no behaviour differences. It sits
underneath other mods and gives them information. If a mod told you to install this,
that is all you need to do.

It does patch PlayMaker, so if you are debugging a conflict this is a reasonable thing
to test without.

## For mod developers

Three tiers of access, all scoped to a single enemy **instance** — never to a kind of
enemy, never globally. Two mods can hold different enemies in the same room without
fighting.

| Tier | Can | Exclusive? |
|---|---|---|
| **Observe** | Read the profile, subscribe to state changes | No |
| **Influence** | Tune speeds, timings, damage, branch weights | No |
| **Override** | Suppress the enemy's decisions and fire transitions | Yes, per instance |

```csharp
[BepInDependency("com.faaris.enemybehaviorapi")]
public class MyMod : BaseUnityPlugin
{
    void Awake()
    {
        EnemyBehavior.EnemyAppeared += enemy =>
        {
            var handle = EnemyBehavior.Claim(enemy, MyGuid, AuthorityTier.Observe);
            _handles[enemy.InstanceId] = handle;

            foreach (var attack in handle.Profile.Actions)
                Logger.LogInfo($"{attack.DisplayName}: {attack.Shape}, {attack.DamageAmount} dmg");
        };
    }
}
```

Always dispose the handle — disposing reverts every parameter this handle wrote and
releases Override.

Full docs, the annotation format for correcting misclassified bosses, and a worked
sample exercising all three tiers:
**https://github.com/faarisaahmed/enemy-behavior-api**

## Status — read this before building on it

**Alpha. The schema is not frozen; expect breaking changes.** Check
`SchemaVersion.IsCompatible` at startup.

Discovery and the Observe tier are measured across 18 enemy kinds, 16 scenes and 2
bosses. Influence and Override work, but carry far less mileage — each has had one real
bug found and fixed by hand-driving an enemy.

Known-wrong things, so they do not surprise you:

- `DamageAmount` reads 0 for most attacks. Silksong keeps hero damage on persistent
  `DamageHero` components rather than in FSM actions.
- Speed resolves for about a third of movements.
- Classification is tuned against early-game enemies. Late-game and Act 3 are unmeasured.
- Contact-damage enemies (Bone Roller) report 0 attacks — correct by the current
  definition, but the schema cannot yet express "hurts to touch".
- `Fsm.SetState` bypasses Override suppression, so an enemy driving itself that way
  keeps going even under `SuppressAll`.

If a classification looks obviously wrong for an enemy you know well, say so on the
GitHub issues — that judgement is the ground truth the classifier gets tuned against.

## Multiplayer

If your mod replays enemy state from elsewhere — applying an authoritative host's FSM
state, a replay, a save restore — wrap it, or an Override claim will veto it and desync
the enemy:

```csharp
using (EnemyBehavior.AuthoritativeScope())
{
    fsm.SendEvent(replicatedEvent);
}
```
