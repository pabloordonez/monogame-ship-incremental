# P2_FLIGHT_COMBAT Contract Change Proposal

## Status

No P0 shared-contract mutation is required for this package delivery.

## Decision

P2 ships a self-contained `FlightCombatSimulation` that owns:

- `FlightCommandFrame` and `FlightAction` (package-owned command surface)
- combat components, spatial index, ordered damage, weapons, mobility, AI, threat director
- immutable `CombatEvent` traces and deterministic state hashes
- MVP behavior registrations via `FlightCombatBehaviorRegistry.CreateMvp()`

P0 `FoundationSimulation`, `CommandFrame`, ECS `World`, content contracts, host shell, persistence, and telemetry are unchanged. Architecture schedule tests for the P0 five-system schedule remain valid because FlightCombat does not register into that scheduler.

## Future shared-contract work (not implemented here)

When a later package composes flight combat into the live host/run loop, affected packages should review:

1. Extending or replacing P0 `CommandFrame` with action bits (`Fire`/`Mine`/`Mobility`/`Interact`) and replay/hash version impact.
2. Inserting FlightCombat systems into the authoritative run scheduler at documented positions from `docs/mvp/systems.md`.
3. Shared presentation/telemetry event catalogs for combat cues.

Until then, Game adapters (`FlightInputAdapters`, `CombatPresentationBinding`) translate into/out of the package-owned simulation without mutating foundation types.
