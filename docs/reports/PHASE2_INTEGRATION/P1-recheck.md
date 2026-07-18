# P1_CONTENT_ART Independent Recheck Report

**Package:** `P1_CONTENT_ART`  
**Reviewer posture:** Independent, read-only; no source edits; no commits; no report written to disk.  
**Pinned base:** `0b12902972d5a98ff785c78a9e0c10728b2a2df0`  
**Original blocked candidate:** `20a823e3be2eccd140f58729e0f883bec0cc8128`  
**Remediation implementation SHA under test:** `7d09a96815cb86bfa9fde89b5969e1f514c105af`  
**Prior artifacts:** `docs/reports/P1_CONTENT_ART/reviewer-report.md`, `remediation-report.md` (on evidence tip `588fced`)  
**Recheck date:** 2026-07-18

## Scope

Verify each prior **BLOCK/HIGH** finding (F1–F5) is fixed with evidence; re-run `scripts/test.ps1`, content clean + incremental builds; confirm `ShipGameHost.cs` has no P1 catalog edits vs base; confirm `ContentBuildPlan` fail-closed restored; confirm art/palette/grayscale tests exist.

## Gate re-run (at `7d09a96`)

| Command | Exit | Observed |
|---|---:|---|
| `powershell -NoProfile -File scripts/build-content.ps1` | **0** | Catalog `mvp-p1-v1` fingerprint `4ecbb2e7e8584f94f0ec52ac6e0b28811606425cb33db986c64a892ab51aa955`; **16 succeeded, 0 failed** |
| Incremental: `dotnet run --project tools/ShipGame.ContentBuilder/... --configuration Release --no-build` (no `--rebuild`) | **0** | Same catalog fingerprint; **16 succeeded, 0 failed** (cache reuse; no per-item rebuild lines) |
| Post-clean authored-source probe (atlas/texture/sound) | n/a | All **8** compiled kinds: `src=True` and `xnb=True` under `content/generated/DesktopVK/Content` |
| `powershell -NoProfile -File scripts/test.ps1` | **0** | Ecs 10, Telemetry 7, Simulation 12, Persistence 14, Content **19**, Architecture 8, Smoke 1 — all passed |

## Ownership / contract checks (required)

| Check | Result | Evidence |
|---|---|---|
| `ShipGameHost.cs` vs P0 base | **Identical** | `git diff --quiet 0b12902 7d09a96 -- src/ShipGame.Game/ShipGameHost.cs`; no `MvpContent` / `mvp-catalog` / `mvp-p1` references |
| `ContentContracts.cs` vs P0 base | **Identical** | `git diff --quiet 0b12902 7d09a96 -- src/ShipGame.Content/ContentContracts.cs` |
| `ContentBuildPlan.DataSources` fail-closed | **Restored** | Non-`data` kinds throw `asset.unsupported-build-kind` (no silent `continue`); regression `DataSourcesRemainFailClosedForNonDataKinds` |
| P0 source-required validation | **Restored** | `AssetArtifactExists` removed; `.xnb` alone does not satisfy P0 manifest validation (`P0ManifestValidationRequiresAuthoringSourceEvenWhenXnbPresent`) |
| Non-owned paths in remediation delta vs base | **Clean** | Touched paths are P1-owned ContentBuilder / content / `MvpContent` / `MvpPixelGates` / Content tests only (no Game host / no shared contracts) |

## Finding-by-finding disposition

### F1 — BLOCK — P0 `ContentContracts.cs` mutated without proposal → **FIXED**

**Prior defect:** Softened `AssetArtifactExists` + silent skip in `DataSources`.  
**Remediation evidence:** File byte-identical to `0b12902`; fail-closed throw restored; regressions cover both behaviors. No `contract-change-proposal.md` required.

### F2 — BLOCK — Non-owned `ShipGameHost.cs` P1 catalog smoke → **FIXED**

**Prior defect:** `SmokeRunner` called `MvpContentLoader`.  
**Remediation evidence:** Host identical to P0; catalog smoke moved to `CatalogSmokeValidatesRequiredMvpIdsWithoutHostWiring` in Content tests.

### F3 — HIGH — Grayscale / effects-off readability unproven → **FIXED** (residual human effects-off is non-major)

**Prior defect:** No automated palette/silhouette/grayscale gates.  
**Remediation evidence:**
- `src/ShipGame.Content/MvpPixelGates.cs` (palette ≤32, silhouette/alpha, grayscale distinctness)
- `AtlasPixelGatesEnforcePaletteSilhouetteAlphaAndGrayscaleDistinctness` exercised in Content suite (passed)
- `waivers.md` documents residual **human** effects-off / playfeel review before `approved` status — acceptable residual, not critical/major for package acceptance of candidate art

### F4 — HIGH — Multi-frame animation metadata without packed strips → **FIXED**

**Prior defect:** Metadata claimed multi-frame strips; generator drew one tile.  
**Remediation evidence:** Python audit of all atlas JSON: **0** regions with `frames != [0]`; validator requires divisible strip width when count > 1; regression `AnimationMetadataIsHonestSingleFrameUntilStripsArePacked` passed.

### F5 — HIGH — Runtime/generated-root readiness incomplete / false validation → **FIXED**

**Prior defect:** Generated root had `.xnb` without authored sources; P0 validation softened; no generated-root gate.  
**Remediation evidence:**
- Builder post-copies authored sources beside compiled outputs (`Program.cs`)
- Probe after clean build: all atlas/texture/sound `src=True` + `xnb=True`
- `MvpContentLoader.ValidateGeneratedRoot` + `GeneratedRootValidatesAuthoredSourcesAndCompiledXnb` passed
- P0 contracts remain fail-closed (no soft artifact acceptance)

**Note (non-blocking):** `IAssetCatalog` remains `LoadText`-only (P0 surface). Binary kinds are gated by authored-source + `{id}.xnb` presence under generated root, not a new binary load API. That residual is not critical/major given F1/F5 false-readiness path is closed.

## Medium / low carry-forward (not blocking ACCEPT)

| Finding | Severity | Status |
|---|---|---|
| F6 extrusion/padding pixel enforcement | MEDIUM | Unchanged metadata-only; not prior required remediation key |
| F7 hue-only ore risk | MEDIUM | Partially mitigated by shape-distinct draws + grayscale pair tests |
| F8 waiver authority wording | LOW | Unchanged package-local waiver |

## Package-gate scorecard (recheck)

| Gate | Result |
|---|---|
| Ownership boundaries (`ShipGameHost` / `ContentContracts`) | **PASS** |
| `ContentBuildPlan` fail-closed | **PASS** |
| Palette / silhouette / grayscale automated tests | **PASS** |
| Honest animation metadata | **PASS** |
| Clean + incremental content builds | **PASS** |
| Generated-root authored source + xnb | **PASS** |
| Full `scripts/test.ps1` | **PASS** (Content 19) |
| Residual F6–F8 | Open as medium/low only |

## Verdict

**ACCEPT**