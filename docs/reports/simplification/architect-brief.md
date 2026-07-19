# Architect brief — Lean standard ECS (simplification)

**Date:** 2026-07-18  
**Direction (locked):** Keep sparse-set `World`; make combat/mining look like real ECS where it matters; delete ceremony where it does not. Not a full engine rewrite. Not “drop ECS.”

---

## Verdict on claims

| Claim | Verdict | Evidence |
| --- | --- | --- |
| Combat is ECS-shaped but not ECS-driven | **Confirmed** | `FlightCombatSimulation` owns a real `World` + `SystemScheduler`, but every phase is a `DelegateSystem` closure that ignores `world`/`tick` and mutates private fields. No Simulation type implements `ISimulationSystem` except nested `DelegateSystem`. |
| Shadow `_entities` list duplicates store identity | **Confirmed** | Sorted `List<EntityId> _entities` is maintained on create/destroy and is the sole iteration source for timers, AI, movement, spatial rebuild, weapons, mines, hash, snapshots. Stores are lookup-only. |
| Spatial index is coupled to the shadow list | **Confirmed / high risk** | `RebuildSpatialIndex` / `DetectCollisions` index into `_entities` via dense list indices (`_gridHeads` / `_gridNext`). Removing `_entities` requires retargeting the grid to entity IDs or a stable dense remap. |
| Structural mutation is split / ceremonial | **Confirmed** | Combat: `_pendingDestroy` + `ApplyFlightCombatStructuralChanges`; `ResolveCombatDestruction` is an empty placeholder. Foundation: `CommandBuffer _structuralChanges` is never `Enqueue`d; `World _world` never creates entities. Ecs README/docs still prescribe `CommandBuffer` as the sync path. |
| Mining already closer to ECS | **Confirmed** | `CollectionSystem` / orchestrator use `World.Query<Collectible, WorldPosition>()`. Mining/loot are plain classes, not scheduler systems — acceptable for orchestrator-driven resolve. |
| Dead / obsolete scaffolding | **Confirmed** | `LobbyView` obsolete alias of `StationView` (only `MetaSession.Lobby` / `BuildLobbyView`). Host duplicates fixed-step catch-up; `FixedStepDriver` is Foundation-test-only. World-event registry registers ~10 `NoOpWorldEventHandler` instances for presentation-only kinds. |
| Docs invent types / named systems | **Confirmed** | `docs/mvp/systems.md` lists `WorldResource<T>` (does not exist) and named systems (`ThrustSystem`, `AiDecisionSystem`, etc.) that are not types — combat schedule names are the real contract. `technical-architecture.md` implies typed world resources. |
| Weapon/AI strategy registries are load-bearing | **Confirmed — leave** | Closed registries + action surfaces are the extension path; combat “systems” already delegate fire/AI there. |

**Bottom line:** Complexity is real and local: one god-object combat sim with ECS stores underneath a parallel entity roster. Lean ECS means extract systems + iterate stores/queries + one destroy sync path + delete unused ceremony. Do not thicken `ShipGame.Ecs` beyond a thin `Query<T>` (and whatever the spatial-index retarget needs).

---

## Must-change

1. **Combat phases → real `ISimulationSystem` types** that take shared combat context (or read stores from `World`) instead of `DelegateSystem` lambdas over `FlightCombatSimulation`. Keep the same ordered schedule names (tests bind them).
2. **Delete shadow `_entities`.** Iterate `Store<T>().Entities` and/or new `World.Query<T>()` / existing `Query<TA,TB>()`, always **sorted by `EntityId`** for order-sensitive work (hash, AI, weapons, damage adjacency). Retarget spatial grid so it does not depend on `_entities` dense indices.
3. **One structural-mutation path for combat:** prefer `CommandBuffer` applied in `ApplyFlightCombatStructuralChanges` if enqueue cost stays trivial; otherwise **document** `_pendingDestroy` as the combat sync point and remove the empty `ResolveCombatDestruction` no-op from the mental model (keep or fold the schedule slot explicitly). Delete unused Foundation `CommandBuffer` / unused Foundation `World` if session shell no longer needs them.
4. **Delete proven dead scaffolding:** `LobbyView` + lobby builders; trim world-event no-op class explosion via default no-op dispatcher; consolidate fixed-step catch-up if host can call a shared driver without breaking Foundation hash tests.
5. **Align docs** (`systems.md`, `technical-architecture.md`, Simulation/Ecs READMEs): remove `WorldResource<T>`; describe schedule **names** and actual types; state combat destroy sync path truthfully.

## Must-not-change

- Eight production projects + ContentBuilder layout / architecture dependency policy (no project merges).
- Determinism: sorted entity iteration, explicit scheduler order, existing combat schedule name list (unless a schedule rename is tested end-to-end).
- Weapon / AI strategy registries and their action surfaces (out of scope unless blocked).
- Dual C#/JSON catalog unification, Telemetry↔Ecs merge, meta screen registry rewrite, large balance moves.
- Sparse-set `World` / generation `EntityId` model.

---

## Ordered file-level change list

Implement in this order so determinism and spatial index do not regress mid-diff.

### A. Ecs primitives (thin)

1. `src/ShipGame.Ecs/World.cs` — add `Query<T>()` mirroring `Query<TA,TB>` (alive + has, **Order()** snapshot, query-guard).
2. `tests/ShipGame.Ecs.Tests/UnitTest1.cs` — cover single-component query order stability + mutation-during-query still forbidden.
3. Optionally expose a small helper to copy/sort store entities without allocating per system every tick only if combat hot paths demand it (do not invent a second ECS).

### B. Combat: iteration + spatial index (before or with system split)

4. `src/ShipGame.Simulation/Combat/FlightCombatSimulation.cs` — replace `_entities` maintenance in `CreateEntity` / `ApplyStructuralChanges` / all `for (_entities…)` loops with store/query iteration; keep `MaximumEntities` via alive count or a cheap census.
5. Same file — **retarget** `RebuildSpatialIndex` / `DetectCollisions` off `_entities` indices (e.g. parallel entity array for the grid, or map cell → `EntityId` lists sorted before pair emit). Preserve stable pair ordering (`CollisionPairComparer`).
6. Same file — keep hash over a **sorted** live entity set equivalent to today’s BinarySearch-ordered `_entities`.

### C. Combat: real systems

7. New files under `src/ShipGame.Simulation/Combat/Systems/` (or co-located) — one type per schedule phase that currently wraps a private method, e.g. `ApplyFlightCombatStructuralChangesSystem`, `ConsumeFlightCommandsSystem`, … `PublishCombatEventsAndHashSystem`. Each implements `ISimulationSystem` with the **existing** `Name` string.
8. Introduce a narrow `FlightCombatContext` / host interface owned by `FlightCombatSimulation` for command slots, RNG, registries, damage buffers, events, player id — systems must not become a second god-object of public fields.
9. `FlightCombatSimulation` constructor — register concrete systems; delete nested `DelegateSystem`.
10. `tests/ShipGame.Simulation.Tests/FlightCombatTests.cs` — keep `PackageScheduleIsExactOrderedProjectionOfStep`; add/extend hash/replay cases if iteration source changes.

### D. Structural mutation path

11. **Preferred:** wire combat destroys through `CommandBuffer` (`Enqueue` destroy + shadow-list removal / player clear) applied only in the structural phase; drop `_pendingDestroy` if redundant.  
    **Fallback (document in Simulation README):** keep `_pendingDestroy`, delete unused Foundation buffer, note that combat’s sync point is intentional and Foundation’s buffer was dead.
12. `src/ShipGame.Simulation/Loop/FoundationSimulation.cs` — remove unused `_structuralChanges` and unused `_world` **if** schedule can shrink without breaking architecture test `SimulationScheduleIsExplicitAndStable` (update that test + FlightCombat schedule cross-check together). If `ApplyStructuralChanges` remains as a no-op name for compatibility, justify or remove in the same PR.
13. `ResolveCombatDestruction` — either delete from schedule (with test update) or make it the sole apply point; do not leave an empty named phase without a one-line contract comment.

### E. Dead scaffolding

14. Delete `src/ShipGame.Game/Ui/LobbyView.cs`; remove `MetaSession.Lobby` and `MetaUiController.BuildLobbyView`; callers use `StationView` only.
15. `src/ShipGame.Simulation/World/WorldRunEventHandlerRegistry.cs` + `NoOpWorldEventHandler.cs` — default-dispatch missing/presentation kinds to no-op; register only handlers with side effects (`HazardDamage`, `EliteActivation`, `DataCoreDrop`, checkpoint kinds). Keep exhaustive coverage via test that every `WorldRunEventKind` dispatches without throw.
16. `FixedStepDriver` — generalize to `Func`/`Action` step or add a composed-run overload; switch `ShipGameHost` catch-up loop to it **or** document host-local loop and leave driver as Foundation-test utility. Do not break `SameCommandsProduceSameHashesAcrossRenderCadences`.

### F. Docs (same slice or immediate follow-up)

17. `docs/mvp/systems.md` — replace `WorldResource<T>` / fictional system type names with: sparse-set stores, `Query`/`Query<T,U>`, `CommandBuffer` or combat pending-destroy sync, schedule **name** lists for Foundation vs FlightCombat.
18. `docs/mvp/technical-architecture.md` — “typed resources” → owned fields on simulation facades (tick, RNG streams, catalogs), not a `WorldResource<T>` API.
19. `src/ShipGame.Ecs/README.md` / `src/ShipGame.Simulation/README.md` — match the chosen destroy sync path; mention systems live under Combat and read World stores.

### Explicitly out of scope (do not touch in this slice)

- `WeaponStrategyRegistry` / `EnemyAiStrategyRegistry` and strategy classes  
- Meta screen handler registries  
- Catalog/JSON unification, Telemetry project merge  
- Making mining/loot `ISimulationSystem` types unless needed for shared scheduler (orchestrator resolve is fine)

---

## Acceptance criteria

- [ ] No `List<EntityId> _entities` (or equivalent parallel roster) in `FlightCombatSimulation`.
- [ ] Combat schedule still an explicit ordered `ISimulationSystem` list; names match current golden list **or** an intentionally updated golden list with reason.
- [ ] Each combat phase is a named type implementing `ISimulationSystem` (no `DelegateSystem` closures over the facade).
- [ ] Order-sensitive combat paths iterate entities in `EntityId` sort order; existing deterministic FlightCombat tests remain green (hashes stable for same seeds/commands).
- [ ] Spatial collisions still emit stable ordered pairs; no reliance on dense insertion order.
- [ ] Exactly one documented structural destroy apply point for combat; Foundation has no dead never-enqueued `CommandBuffer`.
- [ ] `LobbyView` gone; station UI uses `StationView`.
- [ ] World-event no-ops are default-dispatcher behavior, not a pile of `NoOpWorldEventHandler` types/registrations.
- [ ] Docs no longer mention `WorldResource<T>` or non-existent `*System` type names as if they were APIs.
- [ ] Architecture tests still enforce the 8-project (+ ContentBuilder) layout; Simulation stays MonoGame-free and deterministic-policy clean.
- [ ] Weapon/AI registries unchanged in behavior.

---

## Risks

| Risk | Mitigation |
| --- | --- |
| **Spatial grid uses `_entities` indices** | Retarget grid first or in the same PR as roster deletion; add a collision-order regression test if none asserts pair stability under spawn/despawn churn. |
| **State hash / AI order drift** | Always sort query/store snapshots by `EntityId` (same as today’s BinarySearch list). Compare hashes before/after on existing FlightCombat tests. |
| **System split balloons shared mutable surface** | Keep a single internal context owned by `FlightCombatSimulation`; systems are thin phase owners, not independent services. |
| **`CommandBuffer` of `Action<World>` allocates** | Accept for destroy-rate traffic, or keep `_pendingDestroy` and document; do not add a second unused Foundation buffer. |
| **Foundation schedule rename breaks architecture test** | Update `ArchitectureTests.SimulationScheduleIsExplicitAndStable` and FlightCombat schedule assertion together. |
| **Over-scoping mining into scheduler** | Leave mining as orchestrator-called resolve unless combat/mining share one World (they do not today). |
| **Query allocation per tick** | Existing `Query` already snapshots+Orders; match that. Optimize later only with evidence. |

---

## Recommended decision on structural path

**Prefer documenting combat `_pendingDestroy` + deleting Foundation’s unused buffer** unless the implementer can switch combat destroys to `CommandBuffer` with no hash/perf regression in one PR. Rationale: combat already has a working sync point; Foundation’s buffer is pure ceremony; forcing `Action<World>` enqueue for every destroy is optional consistency, not correctness.
