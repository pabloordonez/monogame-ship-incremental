# P3_WORLD_RUN Remediation Report

**Package:** `P3_WORLD_RUN`  
**Worktree:** `C:\Repositories\github\ship-game-p3`  
**Branch:** `phase2-p3-world-run`  
**Reviewer verdict remediated:** `REMEDIATE` (reviewer-report.md)  
**Remediation candidate:** `9f30f7e6bea9fc3d17f1581b09d99e569bccb485`  
**Date:** 2026-07-18  

---

## Status

`READY_FOR_RECHECK`

## Findings addressed

### 1. CRITICAL — 10k×2 sweep coverage laundering

**Fix:** Renamed/expanded Theory to `TenThousandSeedsPerEnvironmentSatisfyWorldRunInvariants`. For each of 10,000 seeds × 2 environments (20,000 total) the gate now asserts:

| Check | How |
|---|---|
| Validator pass | `EncounterValidator.Validate` |
| OBJ_FIELD_PROOF Ferrite floor | Ferrite cells × 2 ≥ 30 (catalog min yield) |
| Elite + extraction reachability | Validator flood-fill + sector cardinality |
| Reward-bound sanity | Lightweight headless resolution (see honesty note) |

**Honesty note:** Per-seed resolution injects `RunFact`s for combat/mining outcomes and completes a continuous extraction hold. It does **not** run full combat AI, weapon contacts, or enemy pathing. Soft/hard generation fallbacks are tracked in the sweep; natural hard `GenerateFallback` (`Attempt == 4`) remains rare/absent and is proven by a dedicated forced-path test.

Vacuous `Assert.True(fallbackCount >= 0)` removed; fallback counts are bounded telemetry only.

### 2. MAJOR — Extraction hold not continuous

**Fix:** `AdvanceExtraction` resets progress when interact is released **or** the player leaves the zone (matches `game-design.md` six continuous seconds).

**Regression:** `ReleasingInteractInZoneResetsExtractionAndRejectsDiscontinuousHold` — 180 ticks hold → release in-zone → progress 0 + `ExtractionReset`; discontinuous 180+180 does not succeed; only a subsequent continuous full hold succeeds.

### 3. MAJOR — Hard `GenerateFallback` unproven + invalid descriptor

**Fix:**

- Added `invalidateAllAttemptsForTest` to force attempts 0–3 invalid so `GenerateFallback` runs (`Attempt == 4`, `FallbackUsed == true`).
- `HardGenerateFallbackFingerprintIsDeterministic` asserts identical fingerprints across two forced calls (Cinder Belt) and a valid Ion Veil hard fallback.
- Soft retry remains covered by `SoftRetryFallbackFingerprintIsDeterministic` (`Attempt == 1`).
- **Bug found by the new test:** hard fallback had only 12 Ferrite cells (yield floor 24 < 30). Raised to 16 Ferrite / 24 cells so the deterministic fallback validates.

---

## Changed files

- `src/ShipGame.Simulation/WorldGeneration.cs` — hard-fallback test hook; Ferrite floor fix in `GenerateFallback`
- `src/ShipGame.Simulation/WorldRun.cs` — continuous extraction hold reset
- `tests/ShipGame.Simulation.Tests/P3WorldRunTests.cs` — expanded 20k gate, hard fallback, discontinuous hold regression
- `docs/reports/P3_WORLD_RUN/remediation-report.md` (this file)
- `docs/reports/P3_WORLD_RUN/commands-and-results.txt`
- `docs/reports/P3_WORLD_RUN/candidate-commit.txt`

## Evidence

See `commands-and-results.txt`:

- `scripts/test.ps1` exit 0 — **80** passed (Simulation **31**, was 29)
- `P3WorldRunTests` — **19** / 19 passed including 10k×2 world-run invariant sweep (~1–2 s/env)

## Scope

No edits to FoundationSimulation, FoundationContracts, ContentContracts, ShipGameHost, Persistence, or P1/P2/P4 owned paths. No push.
