# P4_META_UI Manual Evidence

## Graphical smoke (automated bounded run)

Command: `scripts/launch.ps1 -WindowSmoke`

Observed console markers:

- `DESKTOPVK_CONTENT_READY`
- `DESKTOPVK_WALKING_SKELETON_COMPLETE`
- Vulkan win32 surface initialized
- GPU: NVIDIA GeForce RTX 3090

Exit code: 0

## Notes

- Meta screen model/navigation is covered by automated `MetaUiTests`; the window smoke still exercises the foundation walking-skeleton host path.
- No separate screenshot capture was required by the package harness.
