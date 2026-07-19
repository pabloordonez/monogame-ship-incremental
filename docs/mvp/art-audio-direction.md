# MVP Art and Audio Direction

## Purpose

Create a readable, inexpensive visual identity for the mechanics test while ensuring candidate assets can be replaced without gameplay changes. Placeholder and generated assets obey final IDs, dimensions, pivots, and pipeline rules.

## Visual pillars

- Crisp top-down pixel art readable during low-attention play.
- Dark, quiet backgrounds contrasted by ships, hazards, resources, and UI.
- Shape and motion communicate state before decoration.
- Effects strengthen impact without hiding hitboxes or warnings.
- A small coherent set is preferable to many inconsistent assets.

## Canvas and rendering

- Virtual gameplay canvas: `640x360`.
- Integer presentation scales: `2x` at 1280x720, `3x` at 1920x1080, `4x` at 2560x1440, and `6x` at 3840x2160.
- At 1080p, each authored pixel becomes a crisp `3x3` display block: visibly pixelated without forcing extremely low-detail source art.
- Letterbox non-matching aspect ratios; never stretch.
- Use `SamplerState.PointClamp`; disable mipmaps and automatic texture resizing.
- Gameplay may use sub-pixel coordinates; round sprite draw positions to virtual pixels.
- Keep gameplay camera zoom discrete when practical. If smooth zoom is introduced later, render through the virtual target and preserve point sampling.
- Optional additive/emissive pass for engines, shields, beams, stars, and pickups.
- Gameplay remains readable with bloom, particles, shake, and flashes disabled.

## Pixel-art specification

### Sizes and silhouettes

- Wayfarer source frame: `64x64`; visible hull should occupy roughly `48–56` pixels on its longest axis.
- Future small ship frame: `48x48`; medium ship frame: `64x64` or `80x80`; large ship frame: `96x96` to `128x128`.
- Normal enemies: `32x32` to `64x64`, selected by role rather than uniformly scaled.
- Elite: same base sprite plus outline/marker; do not require a unique archetype.
- Asteroid chunks: `32x32`, `64x64`, and `96x96`.
- Pickups: `8x8` to `12x12`, differentiated by shape.
- Projectiles: opaque core at least `2x2`; important missiles may use `6x6` to `12x12`; hostile variants need a trail or outline.
- UI icons: authored at `24x24` or `32x32`; detail panels may use `48x48`.
- Every gameplay sprite remains identifiable as a monochrome silhouette.
- Use one consistent pixel density; larger objects get larger canvases, not smaller “pixels.”

### Ship layouts and modular detail

- Author each hull in a standard frame tier with a consistent local-coordinate origin and forward direction.
- Store named hardpoints in metadata: primary weapon, mining tool, utility, left/right engine, shield origin, and optional future mounts.
- Keep collision geometry separate from opaque sprite bounds so distinct silhouettes do not require gameplay special cases.
- Split ship presentation into optional layers where useful: base hull, emissive/engine mask, damage overlay, shield mask, and equipment overlay.
- Equipment changes should appear on the ship only when the module remains readable at normal zoom. Otherwise use a consistent hardpoint effect and HUD icon instead of indistinct visual noise.
- Different hull layouts may reposition hardpoints and collision shapes through data; they must not require subclasses or unique rendering code.
- Preserve at least two pixels of negative space around the visible hull inside its frame for rotation, outlines, and effects.
- Validate every hull at normal camera distance, not only enlarged in an art editor.

### Palette

- Shared recurring palette: at most 32 colors.
- Typical sprite: 4–8 colors.
- Background contrast/saturation remains below interactive objects.
- Cyan: shields/defense.
- Red-orange: damage/hostile fire.
- Yellow: interaction/common salvage.
- Green: repair/confirmed positive state.
- Violet: Lumen/anomalous technology.
- Never communicate ore, hostility, rarity, or availability by hue alone; pair with shape, icon, or pattern.

### Animation and effects

- Idle/engine: 2–4 frames.
- Destruction: 4–8 frames.
- UI feedback: 2–4 frames or code-driven interpolation.
- Standard rates: 6, 8, or 12 FPS, stored in metadata.
- Decorative animation never changes collision.
- Selective one-pixel outlines; lit edges follow the dominant star where practical.
- Damage flashes last at most 100 ms continuously.
- Particles have a configurable cap and degrade gracefully.

## Atlas rules

- Source: lossless RGBA PNG without color-profile dependency.
- Maximum MVP atlas: `2048x2048`.
- Two transparent padding pixels and one edge-extrusion pixel around regions.
- Stable lowercase slash IDs, for example `ships/player/wayfarer`.
- Runtime references region IDs, never rectangles.
- Metadata stores rectangle, normalized pivot, animation, and optional collision reference.
- Normal pivot is `(0.5, 0.5)`.
- Rotated packing is prohibited.
- Repacking may change coordinates, never IDs.

## Source layout

```text
content/
  source/
    textures/
      atlases/
      backgrounds/
      ui/
    effects/
    fonts/
    sfx/
    data/
      asset-manifest.json
  definitions/
  generated/
tools/
  ShipGame.ContentBuilder/
```

Use lowercase kebab-case files and slash-separated IDs. Compiled outputs, intermediates, editor caches, and image-generation scratch files are not committed.

## MVP asset manifest

`content/source/data/asset-manifest.json` is authoritative. Every entry contains:

- `id`: stable slash-separated ID.
- `kind`: texture, atlas, effect, font, sound, or data.
- `source`: path under the source root.
- `status`: placeholder, candidate, or approved.
- `owner`: work package.
- `license`: SPDX identifier or proprietary.
- `attribution` and `sourceUrl` where required.
- Expected dimensions and atlas metadata reference.
- Optional source/artifact hash.
- Notes and restrictions.

CI rejects duplicate IDs/paths, missing files, wrong dimensions, unresolved atlas regions, absent provenance, traversal outside the root, and runtime references absent from the manifest. A candidate/placeholder retained in a playtest build requires a waiver from the integration owner naming the asset ID, reason, replacement criterion, and confirmed license/provenance. No waiver permits unknown or incompatible licensing.

### Required visual content

- Wayfarer hull, engine frames, damage flash, shield mask, and dash/blink frames.
- Pulse, beam, seeker missile, mining beam, and seismic charge.
- Interceptor, Gunship, Sapper, elite outline/marker, and telegraphs.
- Three asteroid sizes with ordinary/Ferrite/Lumen cell variants and break frames.
- Ferrite, Lumen, Data Core, upgrade charge, and extraction marker.
- Cinder Belt and Ion Veil backgrounds, hazards, star glow, and cover cues.
- Hull/shield/resource/module/research/lock/objective/pause/input UI icons.
- Essential impacts, explosions, collection, warnings, UI confirmation, shield, and mining SFX placeholders.

### Generated first-pass art workflow

1. Art implementer creates a contact sheet for palette, silhouettes, scale, and UI language.
2. Generate separate candidate sheets for player/modules, enemies, asteroids/resources, environments, and icons.
3. Pixel-clean generated output manually: enforce grid, palette, alpha, pivots, and silhouette.
4. Split/pack source regions and write metadata.
5. Mark each manifest entry `candidate` with provenance.
6. Test at native virtual resolution, in grayscale, and with effects disabled.
7. Reviewer rejects inconsistent pixel density, unreadable telegraphs, copied visual identity, bad alpha, or missing provenance.

Image generation provides candidates, not final unattended assets. Text inside generated images is prohibited; UI labels use the selected font.

## MonoGame 3.8.5 C# Content Builder

Use the released code-centric Content Builder console project, not legacy MGCB Editor.

- Pin framework and content packages to exact matching 3.8.5 versions.
- Do not add legacy `MonoGame.Content.Builder.Task`, `MonoGameContentReference`, or hand-maintained `.mgcb` as source of truth.
- Source assets remain separate from intermediate and runtime output.
- Build rules are grouped by folder/type and reviewed as code.
- Textures disable mipmaps, power-of-two resize, and lossy conversion.
- Compile effects/fonts/SFX through the pipeline.
- Copy JSON only while runtime parsing is intentional; introduce a custom importer only after measured need.
- Custom processors target Any CPU and bump their version when output meaning changes.
- CI runs clean and incremental content builds.
- Runtime loads extension-free, case-correct asset IDs through `IAssetCatalog`, not scattered `Content.Load` strings.
- Content build returns non-zero on any failed item and validates the manifest before compilation.

The implementation agent must verify exact 3.8.5 APIs against the pinned official package; examples in documentation are not a substitute for a compiling content project.

Official references:

- <https://docs.monogame.net/articles/getting_started/content_pipeline/content_builder_project.html>
- <https://monogame.net/blog/2026-07-15-3.8.5-release-2026/>

## Music commissioning brief

No music file is produced for the MVP. A later commission should create original, non-derivative space-electronic music with:

- Slow analog pads and distant harmonic movement.
- Restrained vaporwave texture without parody.
- Sparse pulses that support concentration.
- Expansive cinematic-synth mood without imitating a named work or recognizable melody.

Requested cues:

- Lobby: seamless 90–150 second loop; calm, safe, anticipatory.
- Flight: seamless 150–240 second loop; spacious, gently propulsive.
- Optional later combat layer compatible with the flight cue.

Delivery requirements:

- Lossless masters at 48 kHz/24-bit and a pipeline-ready format verified against MonoGame 3.8.5.
- Click-free loops, consistent loudness, no clipping, vocals, recognizable samples, or uncleared material.
- Provenance, ownership, license, composer credit, and usage rights recorded in the manifest.
- Separate master/music volume, mute, and persisted settings.
- Transition between lobby and flight within two seconds.
- Music must tolerate pause and 20-minute low-attention sessions without obvious fatigue.

Until commissioned music exists, the game must work with music disabled. Do not commit an unlicensed placeholder.
