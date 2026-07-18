# P1_CONTENT_ART Remediation Report

**Package:** `P1_CONTENT_ART`  
**Worktree:** `C:\Repositories\github\ship-game-p1`  
**Branch:** `phase2-p1-content-art`  
**Pinned base:** `0b12902972d5a98ff785c78a9e0c10728b2a2df0`  
**Blocked candidate:** `20a823e3be2eccd140f58729e0f883bec0cc8128`  
**Remediation implementation SHA:** `7d09a96815cb86bfa9fde89b5969e1f514c105af`  
**Date:** 2026-07-18  
**Verdict:** `READY_FOR_RECHECK`

No shared-contract residual remains; `contract-change-proposal.md` was not required (`ContentContracts.cs` restored to P0).

## Finding-to-fix mapping

| Finding | Severity | Reproduction | Fix |
|---|---|---|---|
| **F1** | BLOCK | Diff softened `ContentBuildPlan.DataSources` (skip vs throw) and added `AssetArtifactExists` to P0 `ContentContracts.cs` without proposal. | Restored `ContentContracts.cs` to P0 baseline (`git checkout 0b12902`). Multi-kind rules stay in `ContentBuildRules`. Regression: `DataSourcesRemainFailClosedForNonDataKinds`, `P0ManifestValidationRequiresAuthoringSourceEvenWhenXnbPresent`. |
| **F2** | BLOCK | `SmokeRunner` in `ShipGameHost.cs` called `MvpContentLoader` (non-owned Game host). | Restored `ShipGameHost.cs` to P0 baseline. Catalog smoke moved to `MvpContentTests.CatalogSmokeValidatesRequiredMvpIdsWithoutHostWiring`. |
| **F3** | HIGH | No automated palette / silhouette / grayscale gates; readability self-admitted incomplete. | Added `MvpPixelGates` + `AtlasPixelGatesEnforcePaletteSilhouetteAlphaAndGrayscaleDistinctness` (palette ≤32, silhouette/alpha occupancy, ore + friendly/hostile projectile grayscale/silhouette distinctness). Shape-distinct ore/projectile draws in `SourceAssetGenerator`. Residual human effects-off review documented in `waivers.md`. |
| **F4** | HIGH | Metadata claimed multi-frame strips; generator drew one tile. | Set all animation `frames` to `[0]` (count 1) with reserved fps; validator allows 1–8 and requires divisible strip width when count > 1. Regression: `AnimationMetadataIsHonestSingleFrameUntilStripsArePacked`. |
| **F5** | HIGH | Generated root had xnb without authored sources; validation softened via P0 contracts; no generated-root gate. | Builder post-copies authored sources beside compiled `{id}.xnb` (without changing P0 contracts). Added `MvpContentLoader.ValidateGeneratedRoot` + `GeneratedRootValidatesAuthoredSourcesAndCompiledXnb`. |

## Medium / low (not blocking recheck)

| Finding | Disposition |
|---|---|
| **F6** MEDIUM extrusion pixels | Unchanged; metadata still asserts padding/extrusion integers. Not in required remediation key list. |
| **F7** MEDIUM hue-only ore risk | Partially addressed by shape-distinct ore/projectile draws + grayscale tests (overlaps F3). |
| **F8** LOW waiver authority | Unchanged wording; package-local waiver retained. |

## Gate evidence (remediation)

| Command | Exit | Notes |
|---|---:|---|
| `scripts/build-content.ps1` | 0 | 16 succeeded, 0 failed; catalog `mvp-p1-v1` |
| ContentBuilder incremental (no `--rebuild`) | 0 | 16 succeeded, 0 failed |
| `scripts/test.ps1` | 0 | Content **19** (was 14); all suites green |
| `scripts/launch.ps1 -Smoke` | 0 | |
| `scripts/launch.ps1 -WindowSmoke` | 0 | `DESKTOPVK_CONTENT_READY`, `DESKTOPVK_WALKING_SKELETON_COMPLETE` |

## Ownership after remediation

| Path | Status |
|---|---|
| `ContentContracts.cs` | Identical to P0 (`0b12902`) |
| `ShipGameHost.cs` | Identical to P0 (`0b12902`) |
| P1-owned ContentBuilder / MvpContent / MvpPixelGates / Content tests / source art | Updated |

## Status

**READY_FOR_RECHECK** — do not self-ACCEPT; await independent reviewer.
