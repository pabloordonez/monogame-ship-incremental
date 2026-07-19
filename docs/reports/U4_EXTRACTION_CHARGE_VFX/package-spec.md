# U4_EXTRACTION_CHARGE_VFX Package Specification

## Identity

- Package: `U4_EXTRACTION_CHARGE_VFX`
- Branch: `feat/extraction-charge-vfx`
- Worktree: `ship-game-u4-extraction`

## Goal

When the extraction gate is open and the player is charging (in-zone dwell), show a charge-up animation at the gate using `ExtractionProgressTicks / ExtractionHoldTicks`. Remove the on-screen numeric HUD `Extracting: {progress}/{hold}` from `RunMetaScreen`.

## Scope

- Presentation only: consume existing `ComposedRunHud` extraction fields.
- Do **not** change `WorldRun` timing or extraction logic.

## Ownership

- `src/ShipGame.Game/MvpPresentation.cs` — `DrawExtractionCharge`, progress ratio helper
- `src/ShipGame.Game/Meta/IMetaScreenCanvas.cs` — canvas contract
- `src/ShipGame.Game/Meta/Screens/RunMetaScreen.cs` — gate marker + charge ring wiring
- `tests/ShipGame.Game.Smoke.Tests/ExtractionChargePresentationTests.cs`
- Living doc note in `docs/mvp/game-design.md`

## Design

- At the extraction marker: pulsing `telegraphs/mine-radius` ring plus rotating pixel arc scaled by progress (0..1).
- Progress 0 (gate open, not in zone): idle pulsing ring only; no numeric HUD.
- Off-screen: existing edge ping unchanged.

## Gates

- No `Extracting: N/M` text during extraction phase.
- Charge visuals reflect HUD progress ratio without altering simulation.
- Game smoke tests for presentation helpers pass; build succeeds with 0 warnings/errors.

## Evidence

`implementer-report.md`, `reviewer-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, `manual-evidence/README.md`.
