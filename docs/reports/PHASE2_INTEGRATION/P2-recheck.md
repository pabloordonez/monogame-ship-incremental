# P2_FLIGHT_COMBAT Independent Recheck Report

## Identity

| Field | Value |
|---|---|
| Package | `P2_FLIGHT_COMBAT` |
| Worktree | `C:\Repositories\github\ship-game-p2` |
| Branch | `phase2-p2-flight-combat` |
| Pinned base | `0b12902972d5a98ff785c78a9e0c10728b2a2df0` |
| Original candidate | `724079562a565e9585096d0a926d931320df3a50` |
| Remediation SHA (verified) | `6c5ffc0be6103bc2d806217f9c4adc820db0f027` |
| Evidence tip (not under test) | `8da816952a86bda028aaaf15f9e1164e2c59deb5` (report SHA only) |
| Prior verdict | `REMEDIATE` |
| Recheck mode | READ-ONLY; no package commits; probes ran against built remediation assemblies |

## Scope of recheck

Verified the three majors from `reviewer-report.md` against remediation claims in `remediation-report.md`:

1. **M1** — `Schedule` == live `Step()` phase order  
2. **M2** — zero steady-state alloc under continuous `Queue`+combat  
3. **M3** — pending commands included in authoritative hash  

Minors m1/m2 claimed fixed; m3/m4 left open by remediation (not re-elevated).

---

## Command evidence (this recheck)

### `scripts/test.ps1`

Exit `0`. Build: **0 Warning(s), 0 Error(s)**.

| Suite | Result |
|---|---|
| ShipGame.Ecs.Tests | Passed: 10 |
| ShipGame.Telemetry.Tests | Passed: 7 |
| ShipGame.Content.Tests | Passed: 8 |
| ShipGame.Persistence.Tests | Passed: 14 |
| ShipGame.Game.Smoke.Tests | Passed: 3 |
| ShipGame.Architecture.Tests | Passed: 8 |
| ShipGame.Simulation.Tests | Passed: 33 |
| **Total** | **83 passed / 0 failed** |

### FlightCombat filters

```
dotnet test ...Simulation.Tests --filter "FullyQualifiedName~FlightCombat" -c Release --no-build
→ Passed: 21

dotnet test ...Game.Smoke.Tests --filter "FullyQualifiedName~FlightCombat" -c Release --no-build
→ Passed: 2
```

---

## Finding disposition

### M1 — Schedule ≠ Step order — **RESOLVED**

**Code:** Constructor registers phases on `SystemScheduler`; `Schedule = _scheduler.Order`; `Step()` only runs `_scheduler.Tick` (plus damage-buffer bookkeeping / `Tick++`). Order includes `ResolveMobility` before `IntegrateFlightMovement`, and `ResolveMines` after `ResolveWeapons`.

**Regression:** `PackageScheduleIsExactOrderedProjectionOfStep` binds the canonical 14-phase list and asserts P0 foundation schedule unchanged.

**Independent probe:**
- `ScheduleEqualsCanonical=True`
- `MobilityBeforeMovement=True`
- `MinesAfterWeapons=True`

Note: `ResolveCombatDestruction` remains a deferred-removal no-op, but it is now an **explicit** scheduled phase (no longer a hidden/mismatched step). Acceptable for schedule honesty.

### M2 — SortedDictionary command-queue alloc — **RESOLVED**

**Code:** `SortedDictionary` replaced by bounded slot map (`FlightCommandFrame[CommandSlotCount]` + occupancy flags; `CommandSlotCount = CommandHorizonTicks + 1`).

**Package gate:** `WarmIdleAndQueuedCombatHaveZeroSteadyStateAllocation` — idle 20k = 0 B; Queue+Step×5k with obstacle = 0 B; no-Fire Queue+Step = 0 B; Fire may allocate on projectile spawn.

**Independent probe (same warmup protocol as original falsification):**

| Workload | Allocation |
|---|---|
| Idle `Step` × 20,000 | **0 B** |
| `Queue`+`Step` move/aim + obstacle × 5,000 | **0 B** (~0.0 B/tick) |
| Neutral `Queue`+`Step` × 5,000 | **0 B** (~0.0 B/tick) |
| Aim `Queue`+enemy, no Fire × 2,000 | **0 B** (~0.0 B/tick) |
| Fire `Queue`+enemy × 2,000 | 32 B total (no steady ~72 B/tick); original defect eliminated |

Prior reviewer failure (~72 B/tick under Queue) is gone. Residual Fire-path spawn noise is documented and non-command-queue.

### M3 — Pending commands omitted from hash — **RESOLVED**

**Code:** `CalculateHash` mixes `_pendingCommandCount` and every occupied slot in ascending target-tick order (all command fields). Successful and rejected `Queue` refresh `LastStateHash` immediately.

**Regression:** `PendingCommandsAreIncludedInAuthoritativeHash`.

**Independent probe:**
- Equal after shared `Step`
- Future `Fire` Queue → **immediate** hash divergence (`878BF3F9FCD37D1C` vs `50C7F33B728055CB`)
- Still diverge after next `Step`
- Different pending actions diverge
- Stale reject refreshes hash

---

## Minors (informational)

| ID | Status |
|---|---|
| m1 insertion-stability rename | Remediated (`SameSpawnOrderIsDeterministicAcrossRuns`) — not re-falsified |
| m2 stale/future rejection tests | Remediated (`StaleAndFutureCommandsAreRejected`) — probe confirms reject + hash update |
| m3 `FlightAction.Mine` no-op | Still open — not elevated |
| m4 systems.md coverage gaps | Still open — not elevated |

## Ownership / regression sanity

Remediation touches only package-owned Simulation sources + FlightCombat tests + reports. P0 foundation schedule still asserted unchanged. Suites green.

---

## Gate matrix (post-remediation)

| Gate | Verdict |
|---|---|
| Explicit schedule / Schedule ↔ Step | **Pass** |
| Zero steady-state alloc under Queue+combat | **Pass** |
| Authoritative hash includes pending commands | **Pass** |
| Full suite + FlightCombat filters | **Pass** |
| Ownership / no MonoGame in Simulation | **Pass** (unchanged) |

---

## Verdict

**ACCEPT**