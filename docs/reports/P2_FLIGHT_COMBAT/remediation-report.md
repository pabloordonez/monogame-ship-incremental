# P2_FLIGHT_COMBAT Remediation Report

## Status and identity

`READY_FOR_RECHECK`

- Worktree: `C:\Repositories\github\ship-game-p2`
- Branch: `phase2-p2-flight-combat`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Original candidate: `724079562a565e9585096d0a926d931320df3a50`
- Remediation commit: `6c5ffc0be6103bc2d806217f9c4adc820db0f027`
- Reviewer verdict: `REMEDIATE` (`docs/reports/P2_FLIGHT_COMBAT/reviewer-report.md`)
- Evidence HEAD at review: `e69bb8a0c3fafee287c29701146d3f005904d3d2`

This report does not accept the package. Independent recheck is required.

## Finding-to-fix mapping

### M1 — Schedule ≠ Step order

- `FlightCombatSimulation` now registers phases on `SystemScheduler` and `Step()` only runs `_scheduler.Tick`.
- `Schedule` is captured from that registration (exact ordered projection).
- Order includes `ResolveMobility` before `IntegrateFlightMovement`, and `ResolveMines` after `ResolveWeapons`.
- Regression `PackageScheduleIsExactOrderedProjectionOfStep` asserts the canonical list; drifting registration order fails the test. P0 foundation schedule remains untouched.

### M2 — SortedDictionary command queue allocation

- Replaced `SortedDictionary<long, FlightCommandFrame>` with a bounded slot map (`CommandSlotCount = CommandHorizonTicks + 1`) indexed by `targetTick % capacity`, with occupancy flags and collision checks.
- Allocation gate extended: warm idle `Step` × 20,000 = **0 B**; warm continuous `Queue`+`Step` (move/aim + obstacle) × 5,000 = **0 B**.
- Unavoidable transient documented in-test: enabling `FlightAction.Fire` allocates when projectiles are created; identical `Queue`+`Step` without Fire stays **0 B**, proving the prior ~72 B/tick growth was command-queue steady-state, not spawn noise.

### M3 — Pending commands omitted from hash

- `CalculateHash` includes `_pendingCommandCount` and every occupied slot in ascending target-tick order (all command fields).
- Successful and rejected `Queue` refresh `LastStateHash` immediately so checkpoints cannot hide divergent futures between steps.
- Regression `PendingCommandsAreIncludedInAuthoritativeHash`: after equal steps, queue a future Fire on one sim → hashes diverge before and after the next step.

### m1 — Misleading insertion-stability test name

- Renamed to `SameSpawnOrderIsDeterministicAcrossRuns` and asserts reverse spawn order produces a different hash (creation-ordered IDs), so it no longer claims insertion-stable traces.

### m2 — Stale/future CommandRejected untested

- Added `StaleAndFutureCommandsAreRejected` covering both rejection details.

### m3 / m4 — not remediated

- `FlightAction.Mine` no-op and missing systems.md combat cases remain minor; left for a later package pass unless recheck elevates them.

## Owned files touched

- `src/ShipGame.Simulation/FlightCombatContracts.cs` — command horizon/slot constants
- `src/ShipGame.Simulation/FlightCombatSimulation.cs` — scheduler-bound Step, slot-map queue, pending-command hash
- `tests/ShipGame.Simulation.Tests/FlightCombatTests.cs` — schedule/alloc/hash/command regressions
- `docs/reports/P2_FLIGHT_COMBAT/remediation-report.md` (this file)
- `docs/reports/P2_FLIGHT_COMBAT/commands-and-results.txt`
- `docs/reports/P2_FLIGHT_COMBAT/candidate-commit.txt`

No P0 / P1 / P3 / P4 shared paths edited.

## Command evidence

See `commands-and-results.txt` (remediation section). Summary:

| Gate | Result |
|---|---|
| `scripts/test.ps1` | Exit 0; 0 warnings; **83** passed / 0 failed |
| FlightCombat Simulation filter | 21 passed |
| FlightCombat Game.Smoke filter | 2 passed |
| Architecture | 8 passed |

## Recheck focus

1. Confirm `Schedule` list matches live `Step` phases (mobility before movement; mines after weapons).
2. Confirm queued combat allocation stays 0 B after warmup (and fire spawn transient remains non-command-queue).
3. Confirm pending-command hash divergence on future Queue without waiting for consume.
