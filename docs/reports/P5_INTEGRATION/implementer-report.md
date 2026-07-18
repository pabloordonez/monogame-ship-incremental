# P5_INTEGRATION Implementer Report

## Assumptions

- Phase 2 packages ACCEPTed and merged at `c9993bbbb38294b167b7bcfcd4bde0a904c26a07`.
- Pre-flight gates green (148 tests, content 16/0, smokes exit 0) with empty-window baseline documented.
- Harness completion is allowed for automated smoke/golden traces; human path uses live input.

## Gate mapping

| Gate | Evidence |
|---|---|
| Fresh + continued full loop, no debug commands | `SmokeRunner.Run` + `P5IntegrationSmokeTests`; golden `P5ComposedRunTests` |
| In-app content visible | `MvpPresentation` draws atlas sprites/panels; window smoke `DESKTOPVK_COMPOSED_LOOP_COMPLETE` |
| Deterministic golden traces | `P5ComposedRunTests` checkpoints + same-seed equality |
| Automated/content/architecture/migration/smoke | Full suite green; content 16/0; headless+window smoke exit 0 |
| Perf/reliability | Filed in `known-issues.md` with severity (not silently waived) |
| Credits/provenance | Manifest + `waivers.md` |
| Playtest checklist + telemetry usable | `playtest-checklist.md`; `JsonLinesTelemetrySink` under save dir |
| No MonoGame in Domain/Ecs/Simulation | Architecture tests pass |

## Files changed (primary)

- `src/ShipGame.Simulation/ComposedRunOrchestrator.cs` (new)
- `src/ShipGame.Simulation/MetaProgression.cs` (`BeginRun`)
- `src/ShipGame.Simulation/FlightCombatSimulation.cs` (snapshot helpers)
- `src/ShipGame.Game/ShipGameHost.cs` (composed host + SmokeRunner)
- `src/ShipGame.Game/MvpPresentation.cs` (new)
- `src/ShipGame.Game/MetaUi.cs` (Launch locks run index)
- `tests/ShipGame.Simulation.Tests/P5ComposedRunTests.cs` (new)
- `tests/ShipGame.Game.Smoke.Tests/P5IntegrationSmokeTests.cs` (new)
- `docs/reports/P5_INTEGRATION/*`

## Contracts

- Added `BeginRun`; composed orchestrator uses accepted P2/P3/P4 APIs + `RewardHandoff`.
- Host durable path: MetaSession / MetaSaveRepository only (P0 SaveRepository removed from live host).

## Risks / deferred

See `known-issues.md` and `extension-note.md`.

## Candidate commit

See `candidate-commit.txt` after commit.
