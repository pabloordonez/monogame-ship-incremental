# Composed window evidence

## Window smoke (automated DesktopVK)

Command: `scripts/launch.ps1 -WindowSmoke`

Markers observed:

```
DESKTOPVK_CONTENT_READY
DESKTOPVK_COMPOSED_LOOP_COMPLETE
```

Exit 0.

## What the composed host draws

Unlike the empty-window baseline (solid clears only), `MvpPresentation` renders:

- Title/Lobby/Map/Loadout/Research/Summary panels with atlas UI icons and ship sprite
- Run: environment background, asteroid/pickup/ship/enemy atlas regions, HUD bar
- Integer scale from 640×360 with PointClamp sampling and letterboxing

Window-smoke auto-drives Title→Lobby→Map→Launch→harness extract→Summary→Lobby while requiring sprite/panel draws (`DrewSpritesThisFrame`) before printing `DESKTOPVK_COMPOSED_LOOP_COMPLETE`.
