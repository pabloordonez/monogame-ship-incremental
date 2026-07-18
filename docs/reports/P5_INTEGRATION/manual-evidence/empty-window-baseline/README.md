# Empty-window baseline (pre-P5)

Pinned tip: `c9993bbbb38294b167b7bcfcd4bde0a904c26a07`

## Observed live host behavior

`ShipGameHost` still runs the P0 walking skeleton:

- **LoadContent:** loads only `data/title-placeholder` JSON for the window title.
- **Update:** Enter / `--window-smoke` Confirm advances `Title → Lobby → Run (180 empty ticks) → Summary → Lobby`. Title+C loads foundation `profile.json`.
- **Draw:** solid clear colors by `AppState` only — Title dark blue, Lobby teal, Run near-black, Summary purple. No sprites, HUD, MetaUi, FlightCombat, or WorldRun.
- **Persist:** foundation `SaveRepository` → `profile.json` with `ProfileSnapshot(seed, runIndex)` and fingerprint `"foundation-catalog-v1"`.

## Window smoke markers (pre-composition)

From `preflight-window-smoke.txt`:

```
DESKTOPVK_CONTENT_READY
DESKTOPVK_WALKING_SKELETON_COMPLETE
```

Exit code 0. These markers prove Vulkan + content pipeline + skeleton state machine — **not** that Phase 2 gameplay is composed on screen.

## Gate status at baseline

| Gate | Result |
|---|---|
| Content rebuild | 16 succeeded / 0 failed |
| Full suite | 148 passed / 0 failed |
| Headless smoke | exit 0 |
| Window smoke | exit 0 + DESKTOPVK markers |

P5 must replace this empty-window path with composed MetaSession + FlightCombat + WorldRun + real presentation.
