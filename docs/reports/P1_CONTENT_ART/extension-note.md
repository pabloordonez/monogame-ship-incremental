# P1_CONTENT_ART Extension Note

## Data-only additions

Future packages may add without ContentBuilder code changes when they:

- Append `kind: data` or `kind: metadata` manifest entries whose sources are JSON under `content/source/` (copied by `ContentBuildRules`);
- Extend `mvp-catalog.json` with new definitions that reuse existing kinds, nonnegative finite values, acyclic research edges, and references that resolve to catalog IDs, atlas region IDs, or manifest asset IDs;
- Add atlas regions to existing sheets **only after** regenerating/packing art and updating metadata while preserving stable region IDs;
- Mark assets `approved` after art review without changing IDs, pivots, hardpoint names, or frame tiers.

Catalog fingerprint changes whenever `mvp-catalog.json` bytes change; consumers must treat fingerprint as authoritative for save/content compatibility checks.

## Additions requiring focused code

- New build kinds beyond `data` / `metadata` / `atlas` / `texture` / `sound` require a reviewed rule in `ContentBuildRules` and ContentBuilder wiring (effects, fonts, video, etc.).
- Changing texture processor meaning (mipmaps, POT, format, alpha) requires bumping `ContentBuildRules.TextureProcessorVersion` and rebuilding outputs.
- New hardpoint names or collision shape kinds need presentation/simulation owner agreement; do not invent gameplay semantics in art metadata alone.
- Per-cue individual WAV files (instead of the essential bank) need manifest entries, cue metadata migration, and audio binding updates in the presentation owner.
- Music requires a later commission per `art-audio-direction.md`; do not add unlicensed placeholders.

## Stable contracts established / extended by P1

- Slash-separated lowercase asset/region IDs; canonical `A-Z0-9_` catalog definition IDs.
- Manifest schema version remains `ContractVersions.Content` (1) with P1 `buildVersion` and provenance fields on `AssetRecord`.
- Atlas packing rules: max 2048, padding 2, extrusion 1, no rotated packing, pivot `(0.5,0.5)`.
- Frame tiers: Wayfarer 64²; enemies 32–64; asteroids 32/64/96; pickups 8–12; UI 24 or 32.
- Animation FPS limited to 6, 8, or 12.
- Runtime catalog load via `MvpContentLoader.LoadAndValidate(sourceRoot, definitionsRoot)`.
- Build plan via `ContentBuildRules.Enumerate`; P0 `ContentBuildPlan.DataSources` remains the data-only subset.
- Runtime/generated roots may satisfy texture/atlas/sound presence via `{id}.xnb`.

## Version / migration obligations

- Content schema meaning change: increment `ContractVersions.Content` and migrate loaders.
- Compatible catalog value/art replacement preserving IDs: update fingerprint only; no content schema bump.
- Breaking region ID or hardpoint rename: treat as content migration; update all referencing definitions and presentation bindings.
- Research graph or cost semantics change: document for persistence/progression owners; may require save migration if unlock state is stored by ID (IDs themselves must remain stable).

## Required tests for extensions

- Manifest duplicate/missing/provenance negative tests.
- Catalog reference, range, and research-cycle negatives.
- Frame-tier / pivot / hardpoint checks for touched atlases.
- Clean + incremental content builds.
- Architecture package allow-list unchanged unless an ADR adds packages.
- Headless smoke loading catalog + generated title asset.

## Prohibited shortcuts

No legacy MGCB as source of truth, no unlicensed assets, no music placeholders, no silent unsupported build kinds, no path escape outside content roots, no gameplay balance changes disguised as art metadata.
