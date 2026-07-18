# P3_WORLD_RUN Manual Evidence

## Graphical smoke (automated bounded window)

Command: `scripts/launch.ps1 -WindowSmoke`

Observed markers:

- `DESKTOPVK_CONTENT_READY`
- `DESKTOPVK_WALKING_SKELETON_COMPLETE`
- Vulkan instance initialized with `VK_KHR_win32_surface`
- Selected GPU reported by runtime (NVIDIA GeForce RTX 3090 on the implementer machine)

## Limitations

- No human visual-quality review of world-run presentation cues was performed; bindings are unit-tested for stable asset/audio IDs only.
- World-run gameplay is not yet driven by the P0 walking-skeleton host loop; smoke proves host/content/Vulkan path still completes after adding Game presentation bindings.
