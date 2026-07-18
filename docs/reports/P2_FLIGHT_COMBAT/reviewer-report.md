# P2_FLIGHT_COMBAT Independent Reviewer Report

## Identity

- Package: `P2_FLIGHT_COMBAT`
- Worktree: `C:\Repositories\github\ship-game-p2`
- Branch: `phase2-p2-flight-combat`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Candidate implementation: `724079562a565e9585096d0a926d931320df3a50`
- Review range: `0b12902972d5a98ff785c78a9e0c10728b2a2df0..724079562a565e9585096d0a926d931320df3a50`
- Evidence HEAD at review time: `3d498572635d8bd5110bdf558a59f603c75d3897` (implementer evidence only; not in implementation diff)
- Reviewer made no tracked edits; no commits; no reports written to disk.

## Diff inspection

```
74828f1 Freeze P2 flight combat package spec
7240795 Implement P2 self-contained flight combat simulation.
```

Files in `0b12902..7240795` (6 files, +2092 lines):

| Path | Status |
|---|---|
| `docs/reports/P2_FLIGHT_COMBAT/package-spec.md` | A |
| `src/ShipGame.Simulation/FlightCombatContracts.cs` | A |
| `src/ShipGame.Simulation/FlightCombatSimulation.cs` | A |
| `src/ShipGame.Game/FlightCombatBindings.cs` | A |
| `tests/ShipGame.Simulation.Tests/FlightCombatTests.cs` | A |
| `tests/ShipGame.Game.Smoke.Tests/FlightCombatBindingsTests.cs` | A |

P0 / P1 / P3 / P4 shared paths untouched (`FoundationSimulation`, `ShipGameHost`, Domain, Ecs, etc.: empty diff).

## Command evidence (this review)

### `scripts/test.ps1`

Exit `0`. Build: 0 Warning(s), 0 Error(s).

| Suite | Result |
|---|---|
| ShipGame.Ecs.Tests | Passed: 10 |
| ShipGame.Content.Tests | Passed: 8 |
| ShipGame.Telemetry.Tests | Passed: 7 |
| ShipGame.Game.Smoke.Tests | Passed: 3 |
| ShipGame.Persistence.Tests | Passed: 14 |
| ShipGame.Architecture.Tests | Passed: 8 |
| ShipGame.Simulation.Tests | Passed: 31 |
| **Total** | **81 passed / 0 failed** |

### FlightCombat-filtered tests

```
dotnet test tests/ShipGame.Simulation.Tests --filter "FullyQualifiedName~FlightCombat" -c Release --no-build
→ Passed! Failed: 0, Passed: 19, Skipped: 0, Total: 19

dotnet test tests/ShipGame.Game.Smoke.Tests --filter "FullyQualifiedName~FlightCombat" -c Release --no-build
→ Passed! Failed: 0, Passed: 2, Skipped: 0, Total: 2
```

### Additional gates re-run

- Architecture: 8 passed.
- `scripts/launch.ps1 -Smoke`: exit 0.
- `scripts/launch.ps1 -WindowSmoke`: exit 0; markers include `DESKTOPVK_CONTENT_READY`, `DESKTOPVK_WALKING_SKELETON_COMPLETE`, Vulkan init.

These exits do **not** establish acceptance. Adversarial probes below falsify schedule honesty, allocation evidence, and hash completeness.

## Ownership check

**Pass.** Candidate only adds package-owned Simulation/Game/tests/report-spec files. No mutation of P0 foundation contracts or P1/P3/P4 paths. MonoGame/`Microsoft.Xna` appears only in `ShipGame.Game` (`FlightCombatBindings.cs`); Simulation assembly has zero MonoGame references. Architecture MonoGame boundary remains intact. Composition into `ShipGameHost` / `FoundationSimulation` is correctly deferred via `contract-change-proposal.md` rather than silent P0 edits.

## Gate-by-gate verdict

| Gate | Claim | Verdict |
|---|---|---|
| Weapons/enemies exercisable with frame-rate-independent tests | Pulse/beam/seeker + 3 enemies + mixed-frame movement test | **Pass (partial)** — behaviors covered; see M1/M4 |
| KB/gamepad parity, damage order, shields, stale, seeker loss, mobility, presentation isolation | Covered by package tests; probes confirm shield boundary, seeker cone reject, presentation non-mutation | **Pass (partial)** — missing stale/future command tests (m2); schedule/hash/alloc fail below |
| Fixed-step combat without steady-state allocation growth | Idle 20k ticks = 0 B; queued commands allocate ~72 B/tick | **Fail** — M2 |
| Package + architecture + full suite + smokes | All green in this review | **Pass (commands)** |
| Ownership / no MonoGame leakage into Simulation / P0 schedule intact | Additive files; Foundation schedule unchanged | **Pass** |
| Explicit schedule / no hidden systems | `Schedule` ≠ `Step()` order | **Fail** — M1 |

## Falsification matrix

| Risk | Result |
|---|---|
| Frame dependence | **Not falsified.** Mixed 144/30/90 vs 60 Hz accumulators yield identical snapshots/hash. |
| Duplicate damage | **Not falsified** for simultaneous distinct sources; same-source duplicates both apply; destroy-once holds. |
| Ordering | Damage sorted by source then target; simultaneous test expects `[first, second, second]`. **Pass.** |
| Stale entities | Damage/snapshot after destroy+remove is safe. **Pass.** Marked-destroyed entities remain snapshotable for one tick (alive + `Destroyed`) — acceptable deferred removal. |
| Seeker | Behind-target / wrong cone does not fire; target loss does not throw (ballistic after lock clear). **Pass.** |
| Shields | 70 dmg → shield 0 / hull 90; recharge starts at delay boundary (+0.2 at tick 180). **Pass.** |
| Input parity | Keyboard/gamepad adapters equal for matched inputs; Fire eclipses Mine (`else if`). **Pass** for parity; Mine is a no-op in sim (m3). |
| Presentation mutation | Binding translate does not change hash/shield/events; cue list rejects mutation. **Pass.** |
| MonoGame leakage | Simulation clean; Game-only XNA input. **Pass.** |
| P0 schedule breakage | Foundation five-system schedule unchanged; architecture test still passes. **Pass.** |
| Explicit FlightCombat schedule | **Falsified** — M1. |
| Allocation under combat input | **Falsified** — M2. |
| Authoritative hash completeness | **Falsified** — M3. |

## Findings

### M1 — MAJOR: advertised `Schedule` does not match `Step()` execution

`FlightCombatSimulation.Schedule` claims abilities resolve in `ResolveWeaponsAndAbilities` **after** `DetectCombatCollisions`. Actual `Step()` runs `ResolveMobility()` **before** `IntegrateMovement()`, and runs unlisted `ResolveMines()` after weapons. `ResolveCombatDestruction` is a no-op.

This violates the project rule of an explicit ordered schedule without hidden phases (`docs/mvp/evolution-strategy.md`, `technical-architecture.md`). `PackageScheduleIsExplicitAndAdditive` only asserts the string list equals itself and that P0’s schedule is unchanged — it does not bind `Schedule` to `Step()`.

**Required fix:** Make `Schedule` an exact ordered projection of `Step()` (including mobility and mines), or drive `Step()` from a real scheduler registration matching that list; add a regression that fails if order drifts.

### M2 — MAJOR: allocation gate is measured only on command-less idle; combat input allocates every tick

Independent measurement (same warmup protocol as the package test):

| Workload | Allocation |
|---|---|
| Warm idle `Step()` × 20,000 | **0** bytes (matches package test) |
| Warm `Queue(neutral)+Step()` × 5,000 | **360,000** bytes (~72 B/tick) |
| Warm fire-with-enemy × 2,000 | **144,000** bytes (~72 B/tick) |

Root cause: `_commands` is a `SortedDictionary` — each queued frame allocates a tree node. Package-spec gate requires combat to sustain the fixed step **without steady-state allocation growth**. Idle-only zero-alloc evidence is misleading for a command-driven combat sim.

**Required fix:** Bound command storage without per-tick node allocation (e.g. ring/slot map by tick), re-measure under queued combat load, and replace/extend the allocation test accordingly.

### M3 — MAJOR: `LastStateHash` omits pending commands

Probe: after equal `Step()`, queue a future `Fire` on sim A only → hashes still equal; after that tick is consumed, hashes diverge. Same class of defect as P0’s authoritative-hash finding: checkpoints can conceal different futures.

**Required fix:** Include deterministically ordered pending commands in the hash (or forbid non-empty pending at hash publication) and add a divergence-sensitivity regression.

### m1 — MINOR: `StaleEntitiesAreIgnoredAndTraceIsInsertionStable` is misleading

The test only proves same-spawn-order determinism. Reverse spawn order produces different hashes (expected with creation-ordered entity IDs) and is never cross-compared. Rename/split tests; do not claim insertion-stable traces unless that invariant is actually asserted.

### m2 — MINOR: stale/future command rejection untested

`Queue` emits `CommandRejected` for stale/future ticks, but no package test covers it despite `systems.md` input-test expectations.

### m3 — MINOR: `FlightAction.Mine` is accepted by adapters and is a simulation no-op

Player Mine never spawns/mines; Fire wins over Mine when both true. Acceptable if reserved for P3 mining composition, but the command surface currently overclaims behavior. Document or wire intentionally.

### m4 — MINOR: several systems.md combat/ability tests absent

No dedicated friendly-fire, shock-transit-once, or tunneling regressions. Current code appears to block same-faction projectile damage and endpoint-only shock; coverage gaps remain.

## Missing / misleading tests

1. Schedule ↔ `Step()` order binding (M1).
2. Steady-state allocation under continuous `Queue` + combat (M2).
3. Pending-command hash sensitivity (M3).
4. Stale/future `CommandRejected` (m2).
5. Overstated insertion-stability test name (m1).

## Downstream impact

- Later packages composing FlightCombat into the host scheduler cannot trust `Schedule` as integration truth.
- Rollback/netcode/replay checkpoints using `LastStateHash` can match while buffered inputs differ.
- Sustained play with real command ingress will allocate every tick despite a green “zero allocation” gate.
- P3 mining/`Mine` composition may discover a dead action bit and Fire/Mine exclusivity surprise.

## Verdict

`REMEDIATE`

Suites and ownership are green, and several adversarial combat behaviors hold, but three major contract/architecture gaps (schedule honesty, combat allocation evidence, pending-command hash) block acceptance. No critical crash/security finding; no unresolved shared-contract BLOCK — remediation within package-owned files is sufficient.

---

## Final acceptance recheck

Independent recheck verdict: **ACCEPT**
Accepted implementation/remediation SHA: `6c5ffc0be6103bc2d806217f9c4adc820db0f027`
Full recheck body: `recheck-report.md`.
