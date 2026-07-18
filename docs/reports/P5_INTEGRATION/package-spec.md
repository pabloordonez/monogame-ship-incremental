# P5_INTEGRATION Package Specification

## Identity

- Package: `P5_INTEGRATION`
- Branch: `phase3-p5-integration`
- Pinned Phase 2 tip: `c9993bbbb38294b167b7bcfcd4bde0a904c26a07`
- Source: `docs/mvp/agent-workflow.md`, Phase 3 P5.

## Ownership

- Assemble accepted P0–P4 packages through their contracts into one playable DesktopVK host.
- Remove duplicate integration logic (dual saves/continue, dual fingerprints in host path).
- Complete fresh-profile and continue paths with real save/load and profile reward commit.
- Golden input traces, balancing configuration notes, release packaging notes, notices, known issues, playtest build readiness.
- Presentation: 640×360 integer scale, atlas/IAssetCatalog-driven sprites and Meta UI screens.
- Keep MonoGame out of Domain/Ecs/Simulation.

## Ordered delivery

1. Pre-flight Phase 2 gates + empty-window baseline evidence.
2. Composed run orchestrator (FlightCombat + WorldRun + RewardHandoff).
3. ShipGameHost rewrite: MetaSession screens, input wiring, MetaSaveRepository sole durable path.
4. Presentation: real UI/sprites for title/lobby/run/summary.
5. Golden input traces (flight, combat, mining/upgrades/extract, fail, research/save/continue).
6. Dedup adapters; smoke/suite updates; packaging/playtest evidence.

## Gates

- Full loop completes twice: fresh and continued profile; no debug commands, save edits, or seed substitutions.
- In-app content is visible: title/lobby/run/summary render real UI/sprites, not solid-color clears.
- Deterministic golden traces reach expected checkpoints.
- All automated, content, architecture, migration, and smoke suites pass.
- Performance/reliability gates pass, or failures filed with traces and severity.
- Asset credits match provenance; candidate art waived only with recorded waivers.
- Playtest checklist and local telemetry are usable.

## Exclusions

- Music files, plugins, deferred galaxy/automation, speculative engine work, MGCB revival.
- Human art promotion from `candidate` to `approved` (waivers only).

## Evidence

Produce `implementer-report.md`, `reviewer-report.md`, `remediation-report.md` (if needed), `recheck-report.md` (if needed), `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, `waivers.md`, and `manual-evidence/`.
