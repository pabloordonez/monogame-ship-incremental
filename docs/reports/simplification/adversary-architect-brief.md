# Adversary architect brief — Lean standard ECS (simplification)

**Date:** 2026-07-18  
**Role:** Independent skeptical review of [architect-brief.md](./architect-brief.md)  
**Locked direction (unchanged):** Keep sparse-set `World`; make combat/mining ECS-credible where it matters; delete ceremony where it does not. Not a full engine rewrite. Not “drop ECS.”

---

## Executive verdict

The architect brief **correctly diagnoses** the real problem: `FlightCombatSimulation` is a god-object with a manually synced shadow entity roster (`_entities`) sitting on top of real ECS stores, plus dead Foundation scaffolding and fictional docs.

It **over-prescribes** two things that look like “standard ECS” but mostly add surface area:

1. **Fourteen new `ISimulationSystem` types + `FlightCombatContext`** — file multiplication without removing complexity; combat phases are not store-driven systems today and will not become so without a much larger rewrite.
2. **Dogmatic deletion of any sorted entity list** — the bug is *incremental shadow sync*, not *sorting*; spatial grid retarget is the only hard part.

**Prefer the smaller change set.** One PR should focus on **(B) iteration + spatial retarget**, **(D) structural-path cleanup**, **(E) LobbyView deletion**, and **(F) doc truth**. Defer system extraction, `Query<T>()`, world-event registry surgery, and host/`FixedStepDriver` consolidation unless a second slice is explicitly approved.

---

## Attack on each major recommendation

### 1. Combat phases → fourteen real `ISimulationSystem` types

| Architect claim | Adversary verdict |
| --- | --- |
| DelegateSystem lambdas are “not ECS-driven” | **Half true, low leverage.** Lambdas already implement `ISimulationSystem`; schedule names are already the contract (`PackageScheduleIsExactOrderedProjectionOfStep`). |
| Named types make combat “real ECS” | **False marketing.** Phases read command slots, RNG, registries, damage buffers, and events owned by the simulation — not `World` queries. Mining’s `CollectionSystem` is the honest pattern: plain class + `World.Query<TA,TB>()`, no scheduler pretense. |
| `FlightCombatContext` prevents a second god-object | **Optimistic.** Context with ~15 subsystems’ worth of mutable state *is* the god-object, now public to 14 types. |

**Evidence:** Constructor registers 14 `DelegateSystem` closures that ignore `world`/`tick` and call private methods (`FlightCombatSimulation.cs` lines 62–75). `FoundationSimulation` uses the **same** nested `DelegateSystem` pattern (lines 25–29) and passes architecture tests — there is no project rule forbidding closures.

**What to cut:** Acceptance criterion “each combat phase is a named type” and file list **C.7–C.9** (14 new files + context).

**What to keep (amended):** Schedule names and order stay frozen. Optionally one of:

- **Do nothing** on type extraction (DelegateSystem is fine; rename class to `ScheduledPhase` if the name bothers reviewers).
- **Single-file compromise:** `Combat/CombatPhases.cs` with 14 `internal sealed` phase types — satisfies “named types” without 14 directories, still questionable value.
- **Extract only if a phase becomes store-query-driven** (future slice).

**14 types simpler?** **No.** It is ~14 files + wiring + context for the same logic. Net complexity moves from one file to many; review burden goes up; determinism risk goes up (shared mutable context).

---

### 2. Delete shadow `_entities`

| Architect claim | Adversary verdict |
| --- | --- |
| `_entities` duplicates store identity | **Confirmed.** Maintained on `CreateEntity` / `ApplyStructuralChanges` via `BinarySearch` + `Insert` / `Remove`. |
| Stores are “lookup-only” | **Confirmed for combat iteration** — every hot loop uses `_entities`, not `Store<T>().Entities`. |
| Replace with `Query<T>()` / store iteration | **Endorse with amendments.** Do not add `World.Query<T>()` unless a single-component query is proven necessary. |

**Spatial grid — high risk, architect is right to flag it:**

```624:636:src/ShipGame.Simulation/Combat/FlightCombatSimulation.cs
    private void RebuildSpatialIndex()
    {
        Array.Fill(_gridHeads, -1);
        Array.Fill(_gridNext, -1);
        for (var i = _entities.Count - 1; i >= 0; i--)
        {
            var entity = _entities[i];
            // ...
            _gridNext[i] = _gridHeads[cell];
            _gridHeads[cell] = i;
        }
    }
```

`_gridHeads` / `_gridNext` index **dense list positions**, not `EntityId`. `DetectCollisions` uses `firstIndex` / `secondIndex` ordering (`secondIndex <= firstIndex` skip) tied to that dense ordering, then `CollisionPairComparer` sorts pairs by entity id.

**Can `_entities` be removed safely?** **Yes, but only with a deliberate retarget in the same change.** Unsafe approaches:

- Iterate `ComponentStore.Entities` directly → **insertion order ≠ `EntityId` sort** → hash / AI / weapon order drift.
- Call `World.Query<TA,TB>()` in every loop → **allocates** (`ToArray` + `Order` per call in `World.cs` 74–77); ~10 loops/tick is a regression vs one sorted list.
- Index `_gridNext` by `EntityId.Index` → **index reuse after destroy** unless cleared every rebuild ( doable because grid is rebuilt every tick).

**Safer retarget (minimum):**

1. **Tick-local dense spatial array** (reused buffers, not incrementally maintained): during `RebuildSpatialIndex`, walk **sorted live collidables** (from one anchor store), assign dense slots `0..n-1` into `_spatialEntities[]`, link `_gridNext[dense]` / `_gridHeads[cell]`. `DetectCollisions` uses dense indices only within that tick’s rebuild — same algorithm, no persistent roster.
2. **Order-sensitive loops** (hash, AI, weapons, timers): iterate sorted live set from **`Transform2` store** (or `Collider`) — `Where(IsAlive).OrderBy(id)` into a **reused** `List<EntityId>` cleared once per tick at schedule entry, *or* sort per phase if entity counts stay bounded and profiling says OK.

**Amend acceptance criterion:** Ban **incrementally maintained** shadow rosters, not tick-derived sorted buffers. Wording: *“No manually synced entity roster on create/destroy; live iteration derives from `World` stores with explicit `EntityId` sort for order-sensitive paths.”*

**`World.Query<T>()` (architect step A.1):** **Defer.** Mining already uses `Query<Collectible, WorldPosition>()`. Combat’s anchor is “all live combatants with transforms,” which is a single-store census + sort — not a missing ECS primitive. Adding `Query<T>()` expands Ecs surface + tests for marginal combat benefit in this slice.

---

### 3. One structural-mutation path (`CommandBuffer` vs `_pendingDestroy`)

| Architect claim | Adversary verdict |
| --- | --- |
| Prefer `CommandBuffer` for combat destroys | **Reject for this slice.** Allocates `Action<World>` per destroy (`CommandBuffer.cs`); destroy volume is bounded but nonzero. |
| Fallback: document `_pendingDestroy` | **Endorse strongly.** Already correct. |
| Delete unused Foundation `CommandBuffer` | **Endorse.** Never enqueued; first schedule phase applies an empty buffer every tick. |

**Combat path today (working):**

- `MarkDestroyed` → `_world.Set(Destroyed)` + `_pendingDestroy.Add` (same tick, logical death).
- Next tick `ApplyFlightCombatStructuralChanges` → `_world.Destroy` + roster remove + player clear.

**`ResolveCombatDestruction` is not a bug — it is a misleading name:**

```797:800:src/ShipGame.Simulation/Combat/FlightCombatSimulation.cs
    private void ResolveDestruction()
    {
        // Destruction is marked in stable damage order and physically removed next tick.
    }
```

Physical removal is **intentionally deferred** to the next tick’s structural phase (comment matches behavior). The architect is right that the schedule slot is ceremony.

**Prefer `_pendingDestroy` over `CommandBuffer`?** **Yes.** Same sync semantics, zero allocation, fewer moving parts. Document in `ShipGame.Simulation/README.md`: *combat uses deferred destroy list; Foundation session shell has no ECS entities.*

**Foundation cleanup — do it, but expect schedule landmine:**

Removing `_structuralChanges` implies removing or renaming **`ApplyStructuralChanges`** — first entry in golden lists:

- `ArchitectureTests.SimulationScheduleIsExplicitAndStable`
- `FlightCombatTests.PackageScheduleIsExactOrderedProjectionOfStep` (also asserts Foundation list)

Update **both tests in the same commit**. New Foundation schedule (4 phases): `ConsumeCommands`, `SessionTransitions`, `RunClock`, `PublishAndHash`.

**`ResolveCombatDestruction`:** **Remove from combat schedule** (13 phases) with golden list update — cleaner than keeping an empty named phase. If replay tooling depends on phase count, document one-line contract in README instead.

**Unused `FoundationSimulation._world`:** Removing it is harmless but **optional**; it does not affect determinism tests. Lowest priority.

---

### 4. Dead scaffolding (LobbyView, world-event no-ops, FixedStepDriver)

#### LobbyView — **Endorse, zero controversy**

- `LobbyView` is an obsolete alias of `StationView` (`LobbyView.cs`).
- `StationMetaScreen` uses `session.Station`; **`MetaSession.Lobby` / `BuildLobbyView` have no callers** outside their definitions.
- Delete: `LobbyView.cs`, `MetaSession.Lobby`, `MetaUiController.BuildLobbyView`. Keep `EnterLobby()` → `EnterStation()` alias if external API stability matters.

#### World-event no-op explosion — **Cut from this slice**

- `WorldRunEventHandlerRegistry.Create()` registers 10 `NoOpWorldEventHandler` instances for presentation-only kinds.
- Registry **requires exhaustive handler map** (throws on missing enum value) — a design choice that works.
- Default-dispatch refactor touches constructor invariants + dispatch + possibly handler tests. **~10 one-liner registrations are not load-bearing complexity** compared to combat refactor risk.

**Amended:** Single shared `NoOpWorldEventHandler` instance registered for all no-op kinds (if touching registry at all) — do not redesign dispatch in the combat PR.

#### FixedStepDriver / ShipGameHost — **Exclude from this PR**

| | `FixedStepDriver` | `ShipGameHost` |
| --- | --- | --- |
| Step target | `FoundationSimulation.Step()` | `ComposedRunOrchestrator` / composed run |
| Tick rate const | `FoundationSimulation.TickRate` | `WorldRunSimulation.TickRate` |
| Catch-up cap | 8 | 8 |
| Overflow drop | **Yes** (`DroppedSeconds`) | **No** |

Consolidation requires generalizing the driver across **different simulation facades** and reconciling drop behavior. `SameCommandsProduceSameHashesAcrossRenderCadences` uses `FixedStepDriver` + `FoundationSimulation` only — host wiring does not affect that test.

**Verdict:** Document duplication in `ShipGame.Game/README.md` or a one-line comment in `ShipGameHost`; **do not generalize driver in the ECS simplification PR.**

---

### 5. Docs alignment — **Endorse (cheap, high value)**

- `docs/mvp/systems.md` line 66 lists `WorldResource<T>` — **does not exist** (distinct from `WorldResourceAmounts` in world-run economy).
- Fictional `ThrustSystem`, `MovementIntegrationSystem`, etc. — combat schedule **names** are the real contract.

Do this in the same slice or immediate follow-up; zero runtime risk.

---

### 6. False “ECS standard” claims in architect acceptance criteria

| Criterion | Adversary amendment |
| --- | --- |
| Each combat phase is a named `ISimulationSystem` type | **Drop.** Schedule names + deterministic order are the contract; type count is not. |
| No “equivalent parallel roster” | **Narrow.** Forbid *incremental* shadow lists; allow tick-scoped sorted buffers derived from stores. |
| `CommandBuffer` as preferred destroy path | **Invert.** `_pendingDestroy` is the combat standard for this codebase; `CommandBuffer` remains the Ecs primitive for *query-safe* structural deferral when needed. |
| World-event no-op default dispatch | **Optional follow-up.** |

Combat will remain **simulation-owned orchestration over ECS stores** — which is honest “lean ECS,” not Bevy/EnTT cosplay.

---

## Architecture-test landmines (concrete)

| Test | Trigger | Mitigation |
| --- | --- | --- |
| `SimulationScheduleIsExplicitAndStable` | Foundation schedule rename/remove `ApplyStructuralChanges` | Update golden list in same PR as Foundation buffer removal |
| `PackageScheduleIsExactOrderedProjectionOfStep` | **Both** Foundation and FlightCombat schedule changes | Update both arrays together; document reason in commit message |
| `SameCommandsProduceSameHashesAcrossRenderCadences` | Accidental `FixedStepDriver` behavior change | Do not touch driver in this PR |
| `ProductionProjectFilesMatchDependencyAndPackagePolicy` | Project merges / new refs | Do not merge projects; new Combat system files stay in Simulation |
| FlightCombat hash/replay tests | Iteration order / spatial pair order drift | Run full simulation test suite; compare hashes before/after on same seeds |
| Ecs query-mutation guard | Destroy during query iteration | Combat already defers destroy; keep deferral |

**Not landmined:** Architecture tests do **not** assert combat schedule, `ISimulationSystem` concrete types, or absence of `DelegateSystem`.

---

## Key questions (required answers)

### Is extracting ~14 `ISimulationSystem` types actually simpler, or just more files?

**Just more files.** Same logic, same mutable state, higher wiring and review cost. Foundation already uses the closure pattern without complaint. **Recommendation:** do not extract in the minimum slice.

### Can `_entities` be removed safely given spatial grid indexing?

**Yes, in one atomic change** that retargets the grid to tick-local dense indices built during `RebuildSpatialIndex` from a **sorted** live-entity walk over stores — not by blindly switching loops to unsorted `Store<T>().Entities`. **Do not** merge roster deletion without spatial retarget.

### Prefer `CommandBuffer` or keep `_pendingDestroy`?

**Keep `_pendingDestroy` for combat.** Delete Foundation’s never-used `CommandBuffer` and its apply phase. Document the combat deferral model. Do not migrate destroys to `CommandBuffer` in this PR.

### Should FixedStepDriver / host consolidation be in this PR?

**No.** Different step targets, different overflow semantics, Game-layer scope. Document-only at most.

### What is the MINIMUM change set that still meets acceptance criteria?

**Amend acceptance criteria first** (see below), then:

| Priority | Change | Files (indicative) |
| --- | --- | --- |
| P0 | Retarget spatial index; remove incremental `_entities`; sorted store-based iteration | `FlightCombatSimulation.cs` |
| P0 | Remove `ResolveCombatDestruction` schedule slot (or document if kept) | same + `FlightCombatTests.cs` |
| P1 | Remove Foundation `_structuralChanges` + `ApplyStructuralChanges` phase | `FoundationSimulation.cs`, architecture + FlightCombat schedule tests |
| P1 | Delete `LobbyView` dead alias | `LobbyView.cs`, `MetaSession.cs`, `MetaUiController.cs` |
| P1 | Doc truth: no `WorldResource<T>`; schedule names not fictional types | `docs/mvp/*.md`, READMEs |

**Explicitly out of minimum slice:**

- 14 combat system types + `FlightCombatContext`
- `World.Query<T>()`
- World-event registry default-dispatch redesign
- `FixedStepDriver` / `ShipGameHost` merge
- Mining → scheduler systems
- Foundation `_world` removal (optional nit)

---

## Counter-brief: safer implementation order

Implement **only** this sequence in PR 1:

### Step 1 — Spatial + iteration (combat, single file)

1. Add reused scratch: `_sortedLive` (list), `_spatialDense` (entity array), keep `_gridHeads` / `_gridNext` sized for max entities.
2. Extract `BuildSortedLiveEntities()` from `Transform2` (or collider) store: alive, not destroyed, sort by `EntityId`.
3. Rewrite `RebuildSpatialIndex` / `DetectCollisions` to use tick-local dense indices over `_spatialDense`.
4. Replace all `_entities` loops with `_sortedLive` (built once at start of `_scheduler.Tick` or lazily once per tick).
5. Remove `CreateEntity` / `ApplyStructuralChanges` roster maintenance; cap `MaximumEntities` via `_sortedLive.Count` or store census.
6. Run `FlightCombatTests` hash scenarios.

### Step 2 — Structural path + schedule honesty

1. Keep `_pendingDestroy`; document in Simulation README.
2. Remove combat `ResolveCombatDestruction` phase + update golden schedule (13 phases).
3. Remove Foundation buffer + first phase; update Foundation golden schedule (4 phases).

### Step 3 — Dead UI alias

1. Delete `LobbyView` chain; screens already use `Station`.

### Step 4 — Docs (same PR or +1 day)

1. Fix `systems.md` / `technical-architecture.md` / Ecs README destroy narrative.

### Deferred to PR 2 (if ever)

- Combat phase type extraction
- `Query<T>()`
- Shared no-op handler instance
- FixedStepDriver generalization

---

## Revised acceptance criteria (minimum)

- [ ] No **incrementally maintained** shadow entity roster in `FlightCombatSimulation` (spatial dense indices may be tick-local only).
- [ ] Combat schedule explicit ordered list; names match golden list **minus** removed no-op `ResolveCombatDestruction` (or documented if retained).
- [ ] Order-sensitive combat paths use **`EntityId`-sorted** live set equivalent to today’s `_entities` order.
- [ ] FlightCombat deterministic tests green (hashes, collisions, schedule binding updated intentionally).
- [ ] Spatial collisions emit stable ordered pairs; no dependence on store insertion order.
- [ ] Combat destroy sync: `_pendingDestroy` applied only in `ApplyFlightCombatStructuralChanges`; documented.
- [ ] Foundation has no never-enqueued `CommandBuffer` or no-op apply phase.
- [ ] `LobbyView` removed; station UI uses `StationView`.
- [ ] Docs no longer describe `WorldResource<T>` or fictional `*System` types as implemented APIs.
- [ ] Architecture tests green; 8-project layout unchanged; weapon/AI registries behavior unchanged.

**Removed from architect list (not required for lean ECS):**

- Named `ISimulationSystem` type per combat phase
- World-event no-op dispatcher refactor
- FixedStepDriver / host consolidation
- `World.Query<T>()` unless a later slice proves need

---

## What to cut vs keep from architect plan

| Architect section | Verdict |
| --- | --- |
| A. `Query<T>()` in Ecs | **Cut** (defer) |
| B. Iteration + spatial retarget | **Keep** — core value |
| C. 14 real system types + context | **Cut** |
| D. `_pendingDestroy` + Foundation buffer removal | **Keep** (prefer pending destroy) |
| E. LobbyView delete | **Keep** |
| E. World-event no-op trim | **Cut** |
| E. FixedStepDriver consolidation | **Cut** (document only) |
| F. Docs | **Keep** |

---

## Bottom line

The architect found the right wound (**shadow `_entities` + ceremonial buffers + lying docs**). The prescribed cure is overspecified: **fourteen facade system types** and **ECS purity theater** buy little and cost determinism review time. 

**Minimum viable lean ECS:** stores drive iteration, spatial grid uses tick-local dense mapping, one documented destroy deferral path, delete dead Foundation buffer and Lobby alias, fix docs. Ship that first.
