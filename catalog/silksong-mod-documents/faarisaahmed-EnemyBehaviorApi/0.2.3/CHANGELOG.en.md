# Changelog

## 0.2.3

**Required if you use this alongside SSMP or any multiplayer mod.**

Replicated transitions are no longer suppressed. Anything replaying enemy state from
elsewhere is not the enemy *deciding* anything, so vetoing it is always wrong — but at
the point the gate sees it, it is indistinguishable from the enemy's own decision.

SSMP replicates enemy state on non-host clients through `Fsm.Event` and
`PlayMakerFSM.SendEvent`, both of which reach `Fsm.DoTransition`. With any Override
claim held locally the gate vetoed them, so the enemy silently stopped receiving state
and desynced — which reads as broken networking rather than an authority conflict.

Wrap replicated transitions in `EnemyBehavior.AuthoritativeScope()` or
`EnemyBehavior.RunAuthoritative(...)`. Scope it tightly: anything inside really does
bypass authority.

## 0.2.2

**Critical fix: Override no longer freezes the game.**

Suppression used to veto `Fsm.SwitchState`. PlayMaker's update loop is
`while (IsSwitchingState) SwitchState(...)`, and `IsSwitchingState` is cleared only by a
`SwitchState` that completes — so a Harmony prefix returning false left the pending state
set and spun the main thread forever.

Suppression now happens at `Fsm.DoTransition`, which returns a bool and runs before
anything is queued, so refusing there simply means the transition never happens.

Consequence: `Fsm.SetState` does not route through `DoTransition`, so an enemy that
drives itself with `SetState` is not suppressed even under `SuppressAll`. That is the
safe direction to be wrong in — slightly less control, rather than a frozen game.

## 0.2.1

First separated release. The survey was split into its own assembly so that
"this cannot touch your enemies" is a property of the binary rather than a promise.

Enemy discovery, FSM scanning, state/action/parameter extraction, per-instance authority
and the event stream, measured across 18 enemy kinds, 16 scenes and 2 bosses.
