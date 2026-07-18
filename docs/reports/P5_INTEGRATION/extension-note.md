# P5_INTEGRATION extension note

## Contract additions

1. **`ProfileAggregate.BeginRun(transactionId)`** — locks/increments `RunIndex` before field entry; idempotent by transaction ID. Called from `MetaSession.Launch`.
2. **`ComposedRunOrchestrator`** — new Simulation composition root co-stepping FlightCombat + WorldRun + mining ECS; maps rewards via existing `RewardHandoff`.
3. **`FlightCombatSimulation.CollectSnapshots` / `IsElite` / `TryGetPlayerAim`** — presentation/orchestration helpers; no rule changes.

## Deferred follow-ups

- Promote save catalog fingerprint to P1 content SHA with dual-accept migration.
- Bitmap font atlas for UI strings.
- Execute filed perf (1080p capture) and 50-run reliability marathon on reference hardware.
- Human promotion of candidate art to `approved`.

## Non-goals preserved

No MonoGame in Domain/Ecs/Simulation; no music files; no plugins; no MGCB revival.
