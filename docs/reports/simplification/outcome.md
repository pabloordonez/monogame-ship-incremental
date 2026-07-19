# Outcome — Lean standard ECS simplification

**Date:** 2026-07-18  
**Pipeline:** Architect → Adversary architect (Composer) → Consensus → Implementer → Adversary review → Apply (noop) → Verify  
**Artifacts:** [architect-brief.md](./architect-brief.md) · [adversary-architect-brief.md](./adversary-architect-brief.md) · [consensus-decision.md](./consensus-decision.md) · [implementer-review.md](./implementer-review.md)

---

## Does the code work?

**Yes.** Full suite via `scripts/test.ps1`:

| Project | Passed |
| --- | ---: |
| ShipGame.Ecs.Tests | 10 |
| ShipGame.Architecture.Tests | 8 |
| ShipGame.Telemetry.Tests | 11 |
| ShipGame.Persistence.Tests | 24 |
| ShipGame.Content.Tests | 19 |
| ShipGame.Game.Smoke.Tests | 20 |
| ShipGame.Simulation.Tests | 73 |
| **Total** | **165** |

Failed: **0**. Build: **0 warnings / 0 errors**.

---

## Follow-up (user feedback): real system extraction

Thin wrapper systems were rejected as still mixing patterns. A second pass moved phase **bodies** into `Combat/Systems/*` with shared `FlightCombatContext`:

| File | LOC |
| --- | ---: |
| `FlightCombatSimulation.cs` (facade) | **280** (was ~1216) |
| `FlightCombatContext.cs` (world + helpers) | 549 |
| `Combat/Systems/*.cs` (13 real systems) | ~519 total |

`FlightCombatSystems.cs` (one-liner wrappers) deleted.

---

## Was the code simplified?

**Yes.** Combat is readable as ordered systems + a thin public facade. Shared helpers live in `FlightCombatContext`; tick phases are not buried in one god file.

### Before / after counts

| Metric | Before | After |
| --- | ---: | ---: |
| `FlightCombatSimulation.cs` LOC | 1207 | 1216 |
| Combat system types | 1 nested `DelegateSystem` × 14 | 13 named types in `FlightCombatSystems.cs` (81 LOC) |
| Combat schedule phases | 14 (incl. empty destruction) | 13 |
| Incremental shadow `_entities` | Yes | **No** |
| Spatial grid index target | `_entities` dense indices | Tick-local `_spatialEntities` |
| Foundation unused `World` + `CommandBuffer` | Present | **Removed** |
| Foundation schedule phases | 5 | 4 |
| `LobbyView` dead alias | Present | **Deleted** |
| Docs claim `WorldResource<T>` | Yes | **No** |
| `World.Query` production sites | Mining only (2) | Unchanged (mining) |
| Net diff (tracked source/docs/tests) | — | +125 / −112 lines |

### Simpler for a new developer? **Yes**

Three concrete before → after examples:

1. **“Where do live combat entities come from?”**  
   Before: a manually synced sorted `_entities` list that could drift from `World` stores, plus BinarySearch insert on create.  
   After: `_sortedLive` is rebuilt from `Store<Transform2>()` and sorted — the store is the source of truth.

2. **“What runs each combat tick?”**  
   Before: 14 anonymous `DelegateSystem` lambdas closing over private methods, one of which did nothing (`ResolveCombatDestruction`).  
   After: 13 named types in one file (`ApplyFlightCombatStructuralChangesSystem`, …) matching the golden schedule; empty phase removed.

3. **“How do destroys sync with ECS?”**  
   Before: docs implied `CommandBuffer` everywhere; Foundation held a never-enqueued buffer; combat used `_pendingDestroy`.  
   After: combat `_pendingDestroy` is the documented sync path; Foundation no longer pretends to own a World/buffer.

### What is still heavy (intentionally deferred)

- Phase method bodies remain in the combat facade (adversary consensus: do not explode into 14 logic files).
- Weapon/AI registries, dual C#/JSON catalogs, meta screen handlers, world-event no-ops — out of scope.
- Redundant `RebuildSortedLive()` calls per phase are correct but not yet cached once-per-tick (nice-to-have).

---

## Consensus acceptance criteria (final)

- [x] No incrementally maintained `_entities` roster
- [x] Tick-local sorted live set + spatial dense remap
- [x] 13 named combat `ISimulationSystem` types; no nested combat `DelegateSystem`
- [x] Foundation cleaned of dead World/CommandBuffer/ApplyStructuralChanges
- [x] `_pendingDestroy` remains and is documented
- [x] `LobbyView` gone
- [x] Docs truthful
- [x] Weapon/AI registries unchanged
- [x] Architecture + Simulation + Ecs + full suite green
