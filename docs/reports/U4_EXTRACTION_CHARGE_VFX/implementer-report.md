# U4_EXTRACTION_CHARGE_VFX Implementer Report

## Status

`IMPLEMENTATION_COMPLETE`

## Requirement-to-change mapping

1. **Charge-up animation at gate:** `MvpPresentation.DrawExtractionCharge` draws an idle/charging ring (`telegraphs/mine-radius`), a progress-scaled outer ring, and a rotating pixel arc keyed to `progressRatio`. `RunMetaScreen` calls it at the extraction world position when on-screen.
2. **Progress source:** `MvpPresentation.ExtractionProgressRatio(progressTicks, holdTicks)` clamps `ExtractionProgressTicks / ExtractionHoldTicks` to `[0, 1]` from existing HUD fields — no `WorldRun` changes.
3. **Remove numeric HUD:** Deleted `DrawText` block rendering `Extracting: {progress}/{hold}` from `RunMetaScreen`.
4. **Living docs:** `docs/mvp/game-design.md` extraction section notes gate ring animation instead of tick HUD.
5. **Tests:** `ExtractionChargePresentationTests` covers ratio clamping edge cases.

## Files touched

| File | Change |
| --- | --- |
| `MvpPresentation.cs` | `DrawExtractionCharge`, arc helper, `ExtractionProgressRatio` |
| `IMetaScreenCanvas.cs` | Interface method |
| `RunMetaScreen.cs` | Wire charge VFX; remove numeric text |
| `game-design.md` | UX note |
| `ExtractionChargePresentationTests.cs` | New smoke tests |

## Out of scope (honored)

- No edits to `WorldRun`, `ComposedRunOrchestrator` timing, or extraction event semantics.
