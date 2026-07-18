# P4_META_UI Implementer Report

## Status

`IMPLEMENTATION_COMPLETE`

Candidate implementation commit: `61e1c80e43fb5964ba03dee736f7a38de8ef76ae`.

## Requirement-to-change mapping

1. **Atomic/idempotent rewards:** `ProfileAggregate.CommitAcceptedReward(RewardProposal)` validates identity, balance accounting, success/failure counters, and environment rules; records distinct `reward` + `reward_run` receipts for exactly-once semantics; rejects overflow/negatives without mutation.
2. **Research graph + Ion Veil:** `ResearchCatalog` encodes all twelve catalog nodes (565/14/15 total cost), acyclic reachability validation, purchases with prerequisite/gate/cost checks, and grants `CAP_TRAVEL_ION_VEIL`. Travel queries `HasCapability(CAP_TRAVEL_ION_VEIL)`, never a research ID.
3. **Loadout:** five-slot `LoadoutSelection`, defaults, unknown/incompatible/locked fallbacks with diagnostics, deterministic `DeriveStatistics`, and preview diffs.
4. **Screens:** `MetaUiController` + `MetaSession` cover Title/Lobby/Map/Loadout/Research/Run/Pause/Summary/Settings navigation, lock explanations, and summaries without rewriting `ShipGameHost`.
5. **Persistence:** `MetaSaveRepository` writes `profile-v2.json` at `MetaSaveSchema.Current = 2`, migrates foundation schema 1, atomic temp/replace/backup, unknown-ID preservation, newer-version rejection, settings/continue recovery tests.
6. **Telemetry:** `MetaTelemetryTranslator` + `ConsentAwareTelemetry` emit consented local events; sink failure sets failed and never throws into play.

## Gate evidence

1. `scripts/test.ps1` exited 0 — build 0 warnings/errors; **86** tests passed across all seven suites.
2. Meta-focused suites: Simulation Meta 10, Persistence Meta 8, Telemetry Meta 4, Game MetaUi 4 — all passed.
3. Headless smoke: `scripts/launch.ps1 -Smoke` exited 0.
4. Graphical smoke: `scripts/launch.ps1 -WindowSmoke` exited 0 with `DESKTOPVK_CONTENT_READY` and `DESKTOPVK_WALKING_SKELETON_COMPLETE`.

Exact commands and results are in `commands-and-results.txt`.

## Contract-change proposals

See `extension-note.md`. Summary:

- Keep foundation `ProfileSnapshot` (seed/runIndex) unchanged for the walking skeleton.
- P4 introduces `MetaProfileSnapshot`, `RewardProposal`, balances/counters/research/loadout/settings in `ProfileContracts.cs`.
- Propose later folding selected meta fields into foundation contracts during `P5_INTEGRATION` with an explicit save migration.

## Version impacts

| Axis | Impact |
| --- | --- |
| Save | P4-owned `MetaSaveSchema.Current = 2` (`profile-v2.json`). Foundation `ContractVersions.Save = 1` unchanged for `profile.json`. |
| Content / Generation / Rng / Replay | Unchanged (`1`). |
| Telemetry | Schema version remains `ContractVersions.Telemetry = 1`; P4 adds additive event names via translator (no schema bump). Future semantic field changes require a telemetry version increment. |

## Risks and limitations

- Foundation host continue path still uses `SaveRepository`/`profile.json`; meta loop uses `MetaSession`/`profile-v2.json`. Integration package should unify entry points.
- Capability grants are derived from research `Grant` strings rather than a persisted capability set; unknown historical grants remain recoverable via purchased research IDs.
- No polished visual UI chrome; screen model and navigation are compositional and tested.

## Scope confirmation

Did not edit `FoundationContracts.cs`, `FoundationSimulation.cs`, `ContentContracts`, `EcsWorld`, `content/`, or P1/P2/P3 simulation files. Did not push.
