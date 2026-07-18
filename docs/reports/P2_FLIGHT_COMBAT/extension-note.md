# P2 Flight Combat Extension Note

## Data-only additions

Using existing P2 behavior contracts, later packages may add:

- additional weapon/enemy definitions through `FlightCombatBehaviorRegistry` as long as IDs stay unique, ASCII, and within the 64-entry bound;
- temporary combat modifier grants that stay inside reviewed numeric bounds;
- spawn anchors and threat director interval/cap configuration within documented caps.

## Additions requiring focused code

- New weapon or enemy behavior kinds require a `WeaponBehavior`/`EnemyBehavior` case, registry validation, and exact cadence/AI tests.
- New command actions require `FlightAction` bits, keyboard/gamepad adapter parity, and replay/hash review if later shared with P0 `CommandFrame`.
- New combat event kinds require immutable `CombatEventKind` values plus read-only presentation cue translation.
- Shared host composition requires the contract-change path in `contract-change-proposal.md` rather than editing P0 schedule ownership ad hoc.

## Stable contracts established by P2

- Package-owned 60 Hz `FlightCombatSimulation` with explicit schedule names and ordered damage/collision resolution.
- Quantized `FlightCommandFrame` with keyboard/gamepad parity adapters.
- Shield-before-hull damage, 0.35 s hull-hit invulnerability, dash/blink mobility with obstruction shortening.
- Pulse/beam/seeker cadence and heat/lock rules matching MVP content catalog values.
- Interceptor/Gunship/Sapper AI, `MOD_ELITE_PROTOCOL` once-per-run elite modifier, threat anchors/caps.
- Immutable combat events and read-only presentation bindings that cannot mutate simulation state.

## Prohibited shortcuts

Do not edit FoundationSimulation/P0 contracts for P2 features, duplicate combat rules in presentation, allow presentation mutation of sim state, or spawn more than one elite per run.
