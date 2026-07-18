# P1_CONTENT_ART Package Specification

## Identity

- Package: `P1_CONTENT_ART`
- Branch: `phase2-p1-content-art`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Source: `docs/mvp/agent-workflow.md`, Phase 2 P1.

## Ownership

- Exclusive: `tools/ShipGame.ContentBuilder/`, `content/source/`, `content/definitions/`, generated asset metadata, and asset/content validation tests.
- Package-owned additions may extend `src/ShipGame.Content/` without editing P0-owned shared contracts.
- Shared contract changes require a proposal; other packages' source and tests are read-only.

## Ordered delivery

1. Manifest/provenance validation.
2. Runtime catalog and ID/reference validation.
3. MonoGame 3.8.5 clean/incremental build rules.
4. Pixel-art contact sheet and palette.
5. Required player, module, enemy, asteroid, resource, environment, UI/icon, telegraph, and essential SFX candidates.
6. Atlas metadata, pivots, hardpoints, animation/collision references, and runtime smoke loading.

## Gates

- Every manifest/catalog ID resolves exactly once and all references/ranges/graphs validate.
- Assets obey the 640x360 virtual canvas, documented frame tiers, pixel grid, palette, alpha, pivot, hardpoint, grayscale, and effects-off readability rules.
- Clean and incremental content builds pass and runtime loading uses stable catalog IDs.
- Provenance/license records and candidate waivers include replacement criteria; no unlicensed assets or music are added.
- Package tests, architecture tests, full suite, headless smoke, and available graphical smoke pass with exact evidence.

## Evidence

Produce `implementer-report.md`, `reviewer-report.md`, `remediation-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, and `manual-evidence/`.
