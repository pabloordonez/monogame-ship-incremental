# U4_EXTRACTION_CHARGE_VFX Extension Note

## Data-only extensions

- Tune ring colors/sizes/pulse rates in `DrawExtractionCharge` without contract changes.
- Swap `telegraphs/mine-radius` for a dedicated extraction atlas region via catalog reference only.

## Future code extensions

- Spawn extraction charge particles in `UpdateRunParticles` on `ExtractionProgressed` events (reuse existing particle presets).
- Show a simplified charge indicator on the off-screen edge ping when progress > 0.
- Add a headless render assertion test if a test harness for `MvpPresentation` drawing is introduced.

## Stable contracts

- `ComposedRunHud.ExtractionProgressTicks` / `ExtractionHoldTicks` remain the single source of truth for charge visuals.
- `IMetaScreenCanvas.DrawExtractionCharge(center, progressRatio, tick)` is the presentation entry point.

## Migration

None.
