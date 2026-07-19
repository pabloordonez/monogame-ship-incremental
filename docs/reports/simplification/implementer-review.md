# Implementer review — Lean standard ECS simplification

**Date:** 2026-07-18  
**Reviewer:** Adversary Implementer-Reviewer  
**Baseline:** [consensus-decision.md](./consensus-decision.md)  
**Scope reviewed:** Working-tree diff (uncommitted) across Simulation, Ecs, Game meta/UI, tests, and MVP docs.

---

## Verdict: APPROVE

The implementer followed the consensus plan. Incremental `_entities` is removed, spatial collision uses a correct dense remap, combat systems are thin named wrappers in one file, Foundation dead scaffolding is gone, `LobbyView` is deleted, docs no longer invent `WorldResource<T>` or fictional system type names, and the full test suite is green (165 tests, including architecture golden schedules and FlightCombat hash/determinism tests).

No must-fix correctness bugs were found.

---

## Checklist (consensus acceptance criteria)

| Criterion | Status | Evidence |
| --- | --- | --- |
| No incrementally maintained `_entities` in combat | **Pass** | Field deleted; `grep _entities` under `FlightCombatSimulation` returns nothing. `CreateEntity` no longer BinarySearch-inserts. |
| Tick-local sorted live set + spatial dense remap | **Pass** | `_sortedLive` rebuilt via `CopyEntitiesTo` + `Sort()`; `_spatialEntities` / `_spatialCount` used in `RebuildSpatialIndex` / `DetectCollisions`. |
| 13 named combat `ISimulationSystem` types, no nested `DelegateSystem` | **Pass** | `FlightCombatSystems.cs` has 13 thin classes; constructor wires them; golden test updated. |
| Foundation: no dead `ApplyStructuralChanges` / unused World+CommandBuffer | **Pass** | `_world`, `_structuralChanges`, and phase removed; architecture golden → 4 phases. |
| `_pendingDestroy` remains combat destroy sync path | **Pass** | `MarkDestroyed` → `_pendingDestroy`; `ApplyStructuralChanges` → `_world.Destroy`; documented in Ecs/Simulation README and `systems.md`. |
| `LobbyView` gone | **Pass** | File deleted; `MetaSession.Lobby` and `BuildLobbyView` removed; `EnterLobby()` alias kept. |
| Docs truthful (no `WorldResource<T>`, no fictional system APIs) | **Pass** | `technical-architecture.md`, `systems.md`, READMEs updated. Schedule phase names match code. |
| Weapon/AI registries unchanged | **Pass** | No diff under weapon/AI registry paths. |
| Tests green | **Pass** | `dotnet test` — 0 failed (Ecs 10, Simulation 73, Architecture 8, others green). |

---

## Detailed findings

### 1. Incremental `_entities` — gone; sync path simplified

**Before:** Shadow roster maintained via sorted insert on create and `_entities.Remove(entity)` on destroy — two sources of truth coupled to spatial grid indices.

**After:** Single census source — `Transform2` component store — copied into `_sortedLive`, sorted by `EntityId` each rebuild. Destroy path is only `_pendingDestroy` + deferred `_world.Destroy` (no roster mutation).

No leftover `_entities` references in combat. The old sync bug class (roster vs store drift) is eliminated.

### 2. Spatial grid — dense remap is correct

**Before:** `_gridNext` / `_gridHeads` indexed into `_entities` list positions (including non-spatial holes).

**After:** `RebuildSpatialIndex` assigns compact `spatialIndex` 0..`_spatialCount`-1, stores entity in `_spatialEntities[spatialIndex]`, chains `_gridNext[spatialIndex]`. `DetectCollisions` iterates `_spatialCount` and resolves `_spatialEntities[firstIndex]` / `[secondIndex]`.

This is strictly cleaner than the old scheme and preserves deterministic pair ordering: spatial slots are assigned in `_sortedLive` order (EntityId-sorted), and `secondIndex <= firstIndex` skip matches prior list-index semantics among spatial entities.

**Destroyed / reuse handling:** Entities marked `Destroyed` during a tick remain in stores and are skipped via `Has<Destroyed>` checks. Physical removal and index reuse happen only in `ApplyStructuralChanges` at the next tick start — same deferred-destroy contract as before. No same-tick create-after-destroy reuse.

**EntityId reuse:** After `_world.Destroy`, components are removed from stores; `CopyEntitiesTo` cannot emit stale IDs. Generation bump + free-list reuse unchanged in `World`.

### 3. Systems — thin wrappers only

`FlightCombatSystems.cs` (81 lines): each class holds `FlightCombatSimulation`, exposes schedule `Name`, delegates to one `internal` phase method. No new logic, no closure capture, no extra indirection beyond the consensus-mandated named types.

Phase bodies remain in `FlightCombatSimulation.cs` as agreed.

### 4. Scope creep — none in code

| Out-of-scope item | Touched? |
| --- | --- |
| `World.Query<T>()` for combat iteration | **No** — combat uses `CopyEntitiesTo` + sort |
| `CommandBuffer` for combat destroys | **No** |
| `FixedStepDriver` / host merge | **No** |
| World-event no-op registry redesign | **No** |
| Weapon/AI registry changes | **No** |
| Phase body file split | **No** |

**`CopyEntitiesTo`** on `IComponentView<T>` / `ComponentStore<T>` is minimal ECS support for the agreed census rebuild (avoids allocating `EntitySnapshot.ToArray()` on every phase). Not the deferred `World.Query<T>()` approach and within spirit of the slice.

### 5. Docs — accurate with minor wording nit

- `WorldResource<T>` correctly removed; simulation state described as facade fields.
- Schedule names in `systems.md` and `Simulation/README.md` match golden tests and constructor registration.
- Combat structural sync (`_pendingDestroy`, not `CommandBuffer`) documented consistently.
- `World.Query<TA,TB>()` listed in `systems.md` as an existing Ecs primitive (it pre-exists in `World.cs`); combat README correctly describes census rebuild, not Query.

**Minor nit (non-blocking):** Simulation README says iteration rebuilds "each phase"; strictly, some phases delegate to helpers (`ResolveMobility` → `ShortenAgainstObstacles`) or use spatial data from a prior phase (`DetectCombatCollisions`). Behavior is correct; wording is slightly broad.

### 6. Determinism — no unsorted iteration risks

All entity loops use `_sortedLive` after explicit `Sort()` on `EntityId`. Collision pairs and damage requests still sorted via existing comparers. Hash tests pass unchanged.

`RebuildSortedLive` called multiple times per tick is redundant work but not nondeterministic.

### 7. `CopyEntitiesTo` — correct and safely used

- Clears destination, preserves/reuses capacity, copies dense store order, no allocation on steady-state path.
- Used only from single-threaded simulation into reused `_sortedLive` buffer.
- Followed immediately by `Sort()` before any outcome-sensitive iteration.

No other `IComponentView<T>` implementers exist; interface change is contained.

### 8. `MaximumEntities` — still enforced

```csharp
if (_world.Store<Transform2>().Count >= FlightCombatConstants.MaximumEntities)
    throw new InvalidOperationException("Combat entity capacity reached.");
```

Semantically equivalent to prior `_entities.Count` check: every combat entity receives `Transform2` on creation; pending-destroy entities still occupy capacity until next-tick `ApplyStructuralChanges` (same as before).

### 9. Foundation cleanup — complete per consensus

Removed unused `World`, `CommandBuffer`, and `ApplyStructuralChanges` phase. Golden test updated to 4 phases. `_scheduler.Tick(null!, Tick)` passes null because Foundation systems never touch ECS — pre-existing `ISimulationSystem` signature retained. Not ideal API hygiene but not a functional defect and outside the locked combat/Foundation scaffolding scope.

Foundation still uses nested `DelegateSystem`; consensus only required named combat systems.

---

## Must-fix items

None.

---

## Nice-to-have (non-blocking)

1. **`FoundationSimulation.Step` null world** — Consider a scheduler overload or a no-world tick path to avoid `null!` now that Foundation has no `World` instance.
2. **Reduce redundant `RebuildSortedLive` calls** — e.g. cache census once per tick at `Step()` start, or once per phase that needs it (mobility helpers currently rebuild independently).
3. **Doc wording** — Simulation README "each phase" → "phases that iterate entities" for precision.
4. **`CopyEntitiesTo` test** — Optional Ecs test asserting copy + sort stability vs `Entities` snapshot (behavior covered indirectly by Simulation hash tests today).
5. **Foundation symmetry** — Future slice could replace Foundation `DelegateSystem` with named one-file systems (not required by this consensus).

---

## Simplicity assessment: **Yes**

**Evidence:**

| Removed | Added |
| --- | --- |
| Incremental `_entities` list + BinarySearch insert | Tick-local `_sortedLive` rebuild (straightforward) |
| `_entities.Remove` on destroy (dual sync) | Store is sole entity census |
| Nested combat `DelegateSystem` + closure registration | 13 one-liner named system classes in one file |
| Empty `ResolveCombatDestruction` phase | (deleted) |
| Foundation unused `World` + `CommandBuffer` + dead phase | Nothing |
| `LobbyView` alias chain | Nothing |

Net: fewer coupled abstractions, one source of truth for iteration, spatial grid decoupled from roster indices, explicit schedule contract preserved in tests. Phase logic size in `FlightCombatSimulation.cs` is unchanged (by design), but the ECS theater around roster maintenance and delegate registration is gone.

---

## Test verification

```
dotnet test — Passed: 165, Failed: 0
```

Includes:

- `SimulationScheduleIsExplicitAndStable` (Architecture)
- `PackageScheduleIsExactOrderedProjectionOfStep` (Simulation)
- FlightCombat movement, AI, threat, hash, and replay scenarios

---

## Summary for apply-changes agent

**No action required.** Merge-ready against consensus acceptance criteria.
