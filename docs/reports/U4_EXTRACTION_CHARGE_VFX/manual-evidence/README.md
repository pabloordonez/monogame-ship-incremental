# Manual evidence — U4_EXTRACTION_CHARGE_VFX

Automated graphical smoke was not captured in the implementer session. Before merge, verify:

1. Launch a run, defeat the elite, and reach extraction phase.
2. Confirm **no** `Extracting: N/M` text appears anywhere on the HUD.
3. At the gate (on-screen): idle pulsing green ring when outside the zone (progress 0).
4. Enter the extraction zone: ring intensifies and arc fills over ~6 seconds.
5. Leave the zone: arc resets; idle ring returns (progress returns to 0).
6. Complete extraction: run succeeds as before.

Suggested command (from repo root, after content build):

```powershell
scripts/launch.ps1
```

Capture a short screen recording or screenshot set showing idle vs charging vs near-complete states.
