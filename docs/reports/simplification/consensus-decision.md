# Consensus decision — Lean standard ECS simplification

**Date:** 2026-07-18  
**Sources:** [architect-brief.md](./architect-brief.md), [adversary-architect-brief.md](./adversary-architect-brief.md)  
**Deadlock rule:** Prefer the smaller change set that still removes combat ECS theater and dead scaffolding.

---

## Agreement

Both architects agree:

1. Shadow incremental `_entities` roster is the real ECS smell; spatial grid is coupled to it.
2. Keep combat `_pendingDestroy`; do **not** force `CommandBuffer` onto combat.
3. Delete Foundation’s never-enqueued `CommandBuffer` / unused `World` and the empty `ApplyStructuralChanges` schedule slot (update golden tests).
4. Remove empty combat phase `ResolveCombatDestruction` (update golden list to 13 phases).
5. Delete `LobbyView` / lobby builders.
6. Fix docs that invent `WorldResource<T>` and fictional system type names.
7. Do **not** merge projects, touch weapon/AI registries, unify catalogs, or redesign meta screens.
8. Do **not** consolidate `FixedStepDriver` into the host in this slice.
9. Do **not** refactor world-event no-op handlers in this slice.
10. Do **not** add `World.Query<T>()` unless proven necessary (not required for this slice).

---

## Resolved disagreement: system extraction

| Position | Claim |
| --- | --- |
| Architect | Extract ~14 `ISimulationSystem` types + context |
| Adversary | Defer; DelegateSystem already satisfies the interface |

**Consensus:** Extract systems **without file explosion**.

- Replace nested `DelegateSystem` with **named** `ISimulationSystem` types living in **one file**: `src/ShipGame.Simulation/Combat/FlightCombatSystems.cs`.
- Each phase is an `internal sealed class` with the existing schedule `Name`.
- Systems hold a reference to `FlightCombatSimulation` (or a narrow internal context) and call the existing phase methods — thin wrappers that remove closure theater without a 14-file rewrite.
- Phase method bodies stay in `FlightCombatSimulation` for this slice; iteration/spatial work changes there.
- This meets the locked “real systems” goal while honoring the adversary’s “don’t multiply files” constraint.

---

## Ordered implementation plan (implementer must follow)

### 1. Spatial + iteration (highest risk)

In `FlightCombatSimulation.cs`:

1. Add reused buffers: `_sortedLive` (`List<EntityId>`), `_spatialEntities` (`EntityId[]` or list) for tick-local dense spatial slots.
2. Each tick (or at start of phases that need it): rebuild `_sortedLive` from a store census (prefer `Transform2` or union of alive entities that matter), **sorted by `EntityId`**, capped by `MaximumEntities`.
3. Retarget `RebuildSpatialIndex` / `DetectCollisions` to index into `_spatialEntities` dense slots built during rebuild — **not** into an incrementally maintained roster.
4. Replace all `_entities` loops with `_sortedLive` (or rebuild-on-demand equivalent).
5. Delete incremental `_entities` field and BinarySearch insert in `CreateEntity`; destroy path only updates `_pendingDestroy` + `_world.Destroy`.
6. Keep `MaximumEntities` enforcement via `_sortedLive.Count` / alive census before create.

### 2. Systems file

1. Create `FlightCombatSystems.cs` with one internal sealed `ISimulationSystem` per remaining schedule name (13 after `ResolveCombatDestruction` removal).
2. Wire them in `FlightCombatSimulation` constructor; delete nested `DelegateSystem`.

### 3. Structural path + Foundation

1. Keep `_pendingDestroy` + `ApplyFlightCombatStructuralChanges`.
2. Remove `ResolveCombatDestruction` registration and empty method.
3. In `FoundationSimulation`: remove unused `_world`, `_structuralChanges`, and `ApplyStructuralChanges` phase.
4. Update:
   - `tests/ShipGame.Architecture.Tests/UnitTest1.cs` → `SimulationScheduleIsExplicitAndStable`
   - `tests/ShipGame.Simulation.Tests/FlightCombatTests.cs` → `PackageScheduleIsExactOrderedProjectionOfStep`

### 4. LobbyView

Delete `LobbyView.cs`; remove `MetaSession.Lobby` and `MetaUiController.BuildLobbyView`. Keep `EnterLobby`→`EnterStation` alias if present.

### 5. Docs

Update:

- `docs/mvp/systems.md`
- `docs/mvp/technical-architecture.md`
- `src/ShipGame.Ecs/README.md`
- `src/ShipGame.Simulation/README.md`

Truthfully describe: sparse-set stores, schedule names, combat `_pendingDestroy` sync, no `WorldResource<T>` API.

### 6. Verify

Run `dotnet test` on Ecs, Simulation, Architecture, and full suite if time allows. Hashes in FlightCombat tests must stay green.

---

## Explicitly out of scope

- `World.Query<T>()`
- `CommandBuffer` for combat destroys
- FixedStepDriver / ShipGameHost merge
- World-event no-op registry redesign
- Weapon/AI registry changes
- Project merges / catalog unification
- Splitting phase method bodies into separate files

---

## Acceptance criteria

- [ ] No **incrementally maintained** `_entities` roster in `FlightCombatSimulation`.
- [ ] Tick-local sorted live set + spatial dense remap used for iteration/collisions.
- [ ] Combat schedule is 13 named `ISimulationSystem` types (no nested `DelegateSystem`); golden test updated.
- [ ] Foundation schedule has no dead `ApplyStructuralChanges` / unused World+CommandBuffer; architecture golden updated.
- [ ] `_pendingDestroy` remains the combat destroy sync path; documented.
- [ ] `LobbyView` gone.
- [ ] Docs no longer claim `WorldResource<T>` or fictional system APIs.
- [ ] Weapon/AI registries unchanged.
- [ ] Architecture + Simulation + Ecs tests green; FlightCombat determinism/hash tests green.
