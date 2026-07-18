# P1_CONTENT_ART Manual Evidence

## Graphical smoke

Command:

```powershell
powershell -NoProfile -File scripts/launch.ps1 -WindowSmoke
```

Observed process output (noninteractive harness):

- Vulkan instance initialized (`volk`, instance version 1.4.303).
- SDL-required extensions include `VK_KHR_surface` and `VK_KHR_win32_surface`.
- `DESKTOPVK_CONTENT_READY` after `LoadContent` validated the generated manifest (compiled `.xnb` + copied JSON).
- Selected GPU: NVIDIA GeForce RTX 3090.
- `DESKTOPVK_WALKING_SKELETON_COMPLETE` after automated title→lobby→run→summary→lobby flow.
- Process exited 0.

## Limitations

- No screenshot capture was attached by the noninteractive agent harness.
- Window smoke proves content load and skeleton flow, not final sprite presentation, grayscale art review, or effects-off readability of every region.
- Human art review remains required before promoting candidates to `approved` (see `waivers.md`).

## Headless smoke

`scripts/launch.ps1 -Smoke` exited 0 after `MvpContentLoader` catalog validation and generated title-placeholder load.
