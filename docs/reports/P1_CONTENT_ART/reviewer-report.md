# P1_CONTENT_ART Adversarial Reviewer Report

**Package:** `P1_CONTENT_ART`  
**Worktree:** `C:\Repositories\github\ship-game-p1`  
**Pinned base:** `0b12902972d5a98ff785c78a9e0c10728b2a2df0`  
**Candidate SHA:** `20a823e3be2eccd140f58729e0f883bec0cc8128`  
**Reviewer posture:** Independent, read-only; no files edited; no report written to disk.  
**Review date:** 2026-07-18

## Scope reviewed

- Diff `0b12902..20a823e` (23 files, +2919 / −19)
- Package spec, implementer report, waivers, extension note, commands evidence
- `docs/mvp/art-audio-direction.md`, `docs/mvp/content-catalog.md`, `docs/mvp/agent-workflow.md` ownership rules
- Re-run of content build (clean + incremental), full tests, headless smoke, DesktopVK window smoke

## Gate re-run (exact results)

| Command | Exit | Observed |
|---|---:|---|
| `powershell -NoProfile -File scripts/build-content.ps1` (hardcodes `--rebuild`) | **0** | Catalog `mvp-p1-v1` fingerprint `4ecbb2e7e8584f94f0ec52ac6e0b28811606425cb33db986c64a892ab51aa955`; **16 succeeded, 0 failed** |
| Incremental: `dotnet run --project tools/ShipGame.ContentBuilder/... --configuration Release --no-build` (no `--rebuild`) | **0** | Same catalog fingerprint; **16 succeeded, 0 failed** (2 items rebuilt on first sequential run after clean: `ui-icons`, `cinder-belt`) |
| `powershell -NoProfile -File scripts/test.ps1` (after sequential clean build) | **0** | Ecs 10, Telemetry 7, Simulation 12, Persistence 14, Content 14, Architecture 8, Smoke 1 — all passed |
| `powershell -NoProfile -File scripts/launch.ps1 -Smoke` | **0** | Content build 16/0; process completed |
| `powershell -NoProfile -File scripts/launch.ps1 -WindowSmoke` | **0** | `DESKTOPVK_CONTENT_READY`, `DESKTOPVK_WALKING_SKELETON_COMPLETE` |

**Note:** A concurrent incremental builder + `scripts/test.ps1` produced MSB3030 missing-content copy failures (32 errors). That is a race from parallel writers, not a sequential-gate failure. Sequential suite is green.

`scripts/build-content.ps1` always passes `--rebuild`; incremental must be invoked via the ContentBuilder CLI directly (as implementer did).

## Diff summary (candidate)

Owned / expected:
- `content/definitions/mvp-catalog.json`, expanded `asset-manifest.json`, atlas JSON/PNG, backgrounds, contact sheet, SFX bank + cues
- `tools/ShipGame.ContentBuilder/*` (builder + `SourceAssetGenerator`)
- `src/ShipGame.Content/ContentBuildRules.cs`, `MvpContent.cs`
- `tests/ShipGame.Content.Tests/MvpContentTests.cs`
- `docs/reports/P1_CONTENT_ART/package-spec.md`

Out of ownership / contract surface:
- `src/ShipGame.Content/ContentContracts.cs` (P0 shared contracts mutated)
- `src/ShipGame.Game/ShipGameHost.cs` (not in P1 owned paths)

## Claims that hold under falsification

- **No MGCB source-of-truth:** no `.mgcb` / `MonoGameContentReference` / `MonoGame.Content.Builder.Task` in non-docs sources; builder uses released ContentBuilder API + `TextureProcessor`/`WavImporter`.
- **No music files:** only `content/source/sfx/essential-cues.wav`; `sfx-cues.json` has `"music": []`; music-forbidden check present for sound IDs containing `music`.
- **Catalog completeness vs `content-catalog.md`:** all 52 catalog IDs from the doc are present; research totals **565 / 14 / 15**; unresolved catalog→region/asset references: **none**. Extra ID: `SHIP_WAYFARER_MK1` (acceptable).
- **Frame-tier metadata (checked tiers):** Wayfarer 64²; enemies 32–64; asteroids 32/64/96; pickups 8–12; UI 32; pivots `(0.5,0.5)`; Wayfarer hardpoints named as required.
- **Provenance fields + waivers:** candidate/placeholder entries have license, provenance, replacement criterion, waiver text; `waivers.md` lists retained candidates.
- **Clean/incremental content builds and automated suites:** pass when run sequentially (evidence above).

## Severity-ordered findings

### F1 — BLOCK — Ownership / process: P0 shared contracts edited without proposal

**Claim falsified:** Package-spec: “Shared contract changes require a proposal; other packages' source and tests are read-only.” Agent-workflow: shared contracts owned by `P0_FOUNDATION`; contract change requires proposal; implement only owned paths.

**Evidence:** Diff edits `src/ShipGame.Content/ContentContracts.cs`:
- `ContentValidator` now uses `AssetArtifactExists` (accepts `{id}.xnb` when authoring source missing).
- `ContentBuildPlan.DataSources` **no longer throws** on non-`data` kinds; it **silently skips** them (P0 previously rejected unsupported kinds).

Implementer report claims “No shared Domain foundation contract changes” but does **not** disclose ContentContracts mutation or provide a contract-change proposal. Extension-note itself prohibits “silent unsupported build kinds,” while the DataSources change introduces silent skipping.

**Reproduction:**
```bash
git diff 0b12902..20a823e -- src/ShipGame.Content/ContentContracts.cs
# Observe AssetArtifactExists + DataSources continue-vs-throw change
# Search reports: no contract-change proposal artifact
```

**Required remediation:** Revert or isolate P0 contract edits; publish explicit proposal with old/new shape, impacted packages, version/migration, compatibility tests; obtain P0 owner acceptance before merge.

---

### F2 — BLOCK — Ownership: non-owned `ShipGameHost.cs` modified

**Claim falsified:** P1 owned paths are ContentBuilder, `content/source/`, `content/definitions/`, asset/content validation tests. `src/ShipGame.Game/` is outside that set; package-spec marks other packages' sources read-only.

**Evidence:** `SmokeRunner.Run` in `ShipGameHost.cs` adds `MvpContentLoader.LoadAndValidate(...)` and early-exit `10` checks. That is host/smoke wiring owned by foundation walking-skeleton surface, not P1 content ownership.

**Reproduction:**
```bash
git diff 0b12902..20a823e -- src/ShipGame.Game/ShipGameHost.cs
```

**Required remediation:** Move MVP catalog smoke assertion into `ShipGame.Content.Tests` / ContentBuilder validation path; restore `ShipGameHost.cs` to P0 baseline unless a cross-package proposal is accepted.

---

### F3 — HIGH — Package art gate “grayscale / effects-off readability” is unproven and self-admitted incomplete

**Claim falsified:** Package-spec / agent-workflow gate: assets obey palette/grid/pivot **and remain readable in grayscale/effects-off**. Art-audio workflow step 6 requires native-resolution grayscale + effects-disabled testing; reviewer must reject unreadable telegraphs / silhouette failures.

**Evidence:**
- No automated grayscale/effects-off/silhouette tests exist.
- Implementer report: “silhouette/grayscale readability needs human art review before `approved`.”
- Manual evidence: window smoke “not … grayscale art review, or effects-off readability.”
- Candidates remain `candidate`/`placeholder` with waivers, but the **package gate still requires the readability rules**, not merely deferred approval status.

**Reproduction:**
1. Open contact sheet / atlases; convert to grayscale; inspect enemies/telegraphs/UI at 640×360 (and 1× virtual).
2. Confirm absence of any test asserting grayscale/effects-off.
3. Read `implementer-report.md` Risks + `manual-evidence/README.md` Limitations.

**Required remediation:** Produce reviewer-consumable grayscale/effects-off evidence (screenshots or automated silhouette/contrast checks) for required visual regions, or narrow the package gate formally via accepted proposal (not unilateral waiver).

---

### F4 — HIGH — Animation metadata claims multi-frame strips that are not packed as pixels

**Claim falsified:** Ordered delivery / art-audio: animation frames with rates 6/8/12 and idle/destruction frame counts; atlas metadata stores animation. Implementer claims atlas metadata with animation rates.

**Evidence:** 28 regions declare multi-frame `animation.frames` (e.g. `asteroids/break` frames `[0,1,2,3]`), but generator draws **one** rectangle per region and invents frame indices without packing distinct pixel frames. Implementer Risks admits: “multi-frame strips are not yet packed as separate pixel frames.” Validator only checks FPS ∈ {6,8,12} and frame-count bounds — it cannot catch missing strip pixels.

**Reproduction:**
```bash
# Inspect any animated region in atlas-*-resources.json / player-modules.json
# Compare SourceAssetGenerator Anim(...) + DrawRegion (single draw, no frame tiles)
```

**Required remediation:** Pack real frame tiles (or stop claiming multi-frame animation in metadata / gate evidence until strips exist).

---

### F5 — HIGH — “Runtime loads MVP asset IDs through the catalog” is incomplete for compiled assets

**Claim falsified:** Agent-workflow / art-audio: runtime loads MVP asset IDs through `IAssetCatalog` (extension-free IDs). Package-spec: runtime loading uses stable catalog IDs.

**Evidence:**
- Generated root contains `.xnb` for atlases/textures/sound; authoring `source` paths are **absent** under `content/generated/DesktopVK/Content`.
- Probe after build: each atlas/texture/sound has `source_exists=False`, `xnb_exists=True`.
- `FileAssetCatalog.LoadText` still resolves `entry.Source` only — cannot load compiled binary assets; smoke/`LoadContent` only load `data/title-placeholder`.
- Softening `AssetArtifactExists` makes **validation** pass for `.xnb` while **catalog load API** remains source-path-based — a false sense of runtime readiness.

**Reproduction:**
```bash
powershell -NoProfile -File scripts/build-content.ps1
python -c "import json; from pathlib import Path; root=Path('content/generated/DesktopVK/Content'); m=json.loads((root/'data/asset-manifest.json').read_text());
[print(a['id'], 'src', (root/a['source']).exists(), 'xnb', (root/(a['id']+'.xnb')).exists()) for a in m['assets'] if a['kind'] in ('atlas','texture','sound')]"
# Observe: src False, xnb True for all compiled kinds
```

**Required remediation:** Extend runtime catalog APIs to resolve compiled outputs by stable ID (or document a scoped P1 exception via proposal); add a smoke/test that loads every required MVP asset ID through the catalog surface, not only title JSON + authoring-root `MvpContentLoader`.

---

### F6 — MEDIUM — Atlas extrusion/padding is metadata-asserted, not pixel-enforced

**Claim falsified:** Art-audio atlas rules: two transparent padding pixels and one edge-extrusion pixel around regions.

**Evidence:** Atlas JSON sets `padding: 2`, `extrusion: 1`; validator checks those integers and forbids rotated packing. `SourceAssetGenerator.Pack` uses `gap = 5` and never implements extrusion pixel copy; only the JSON field is set.

**Reproduction:** Inspect `WriteAtlas` / `Pack` in `SourceAssetGenerator.cs` vs `ValidateAtlases` in `MvpContent.cs`.

---

### F7 — MEDIUM — Ore/hazard differentiation risks hue-only communication

**Claim falsified:** Art-audio palette rule: never communicate ore/hostility/rarity by hue alone; pair with shape/icon/pattern.

**Evidence:** Asteroid ferrite/lumen variants share diamond silhouette; generator overlays a small hue swatch (`Palette[11]` vs `Palette[14]`). Pickup/hazard families are similarly geometric with color-primary distinction. No shape-distinctness test exists.

**Reproduction:** Compare `asteroids/*/ordinary|ferrite|lumen` draws in `DrawRegion` (Family.Asteroid).

---

### F8 — LOW — Waiver authority is self-declared as package integration owner

**Claim falsified (soft):** Art-audio: candidate/placeholder retained in playtest builds requires waiver from the **integration owner**.

**Evidence:** `waivers.md` names “Integration owner: P1_CONTENT_ART package specification.” Workflow’s integration package is `P5_INTEGRATION`. Self-waiver may be acceptable for package-local candidates, but it is not the same as an integration-owner waiver for a playtest build.

---

## Ownership matrix (candidate touchpoints)

| Path | Allowed for P1? | Verdict |
|---|---|---|
| `tools/ShipGame.ContentBuilder/` | Yes | OK |
| `content/source/`, `content/definitions/` | Yes | OK |
| `tests/ShipGame.Content.Tests/` | Yes | OK |
| New `src/ShipGame.Content/{MvpContent,ContentBuildRules}.cs` | Yes (additive) | OK |
| `src/ShipGame.Content/ContentContracts.cs` | No without proposal | **Violation (F1)** |
| `src/ShipGame.Game/ShipGameHost.cs` | No | **Violation (F2)** |
| Domain / Simulation / Persistence / other tests | Untouched | OK |

## Package-gate falsification scorecard

| Gate | Result |
|---|---|
| Manifest/catalog IDs resolve uniquely; refs/ranges/graphs validate | **PASS** (automated + static cross-check) |
| 640×360 / frame tiers / pivots / hardpoints | **PASS** (metadata + loader checks) |
| Pixel grid / palette / alpha / silhouette / grayscale / effects-off | **FAIL** (palette size OK; readability gates unproven — F3/F7) |
| Clean + incremental content builds | **PASS** |
| Runtime loading via stable catalog IDs | **FAIL / incomplete** (F5) |
| Provenance/licenses/waivers; no unlicensed assets; no music | **PASS** (with F8 caveat on waiver authority) |
| Package/arch/full suite + headless + graphical smoke | **PASS** (sequential) |
| No MGCB | **PASS** |
| Ownership boundaries | **FAIL** (F1, F2) |

## Verdict

**BLOCK**

Automated builds/tests/smokes are green and the MVP ID catalog is complete, but the candidate violates package ownership by mutating P0 shared content contracts and the Game host without a contract-change proposal, while still failing to prove required art-readability and true runtime catalog loading for compiled MVP assets. Do not accept until F1–F2 are corrected (proposal + ownership restore) and F3–F5 are remediated or formally re-scoped by accepted contract change.

---

## Final acceptance recheck

Independent recheck verdict: **ACCEPT**
Accepted implementation/remediation SHA: `7d09a96815cb86bfa9fde89b5969e1f514c105af`
Full recheck body: `recheck-report.md`.
