# P3_WORLD_RUN Independent Recheck Report

**Package:** `P3_WORLD_RUN`  
**Stance:** independent, read-only recheck  
**Original candidate:** `6d0f131915263f6f183788e6107617e536519788`  
**Remediation SHA:** `9f30f7e6bea9fc3d17f1581b09d99e569bccb485`  
**Pinned base:** `0b12902972d5a98ff785c78a9e0c10728b2a2df0`  
**Date:** 2026-07-18  

---

## Scope inspected

Diff `6d0f131..9f30f7e` (remediation + review docs):

| Path | Change |
|---|---|
| `WorldRun.cs` | Reset extraction when interact released (or leave zone) |
| `WorldGeneration.cs` | `invalidateAllAttemptsForTest`; hard-fallback Ferrite floor 12→16 cells / 20→24 |
| `P3WorldRunTests.cs` | Expanded 20k gate, hard-fallback test, discontinuous-hold regression |
| Package docs | remediation/commands/reviewer evidence |

No ownership violations observed (Simulation/tests/docs only).

---

## Finding-by-finding recheck

### 1. CRITICAL — 20k gate generation-only (coverage laundering)

| Check | Status |
|---|---|
| Theory renamed to `TenThousandSeedsPerEnvironmentSatisfyWorldRunInvariants` | **PASS** |
| Per-seed validator + Ferrite floor (≥30 min yield) | **PASS** |
| Elite/extraction sector cardinality + flood-fill reachability | **PASS** |
| Per-seed lightweight headless reward resolution (objective facts → elite → continuous extraction → `Success` + banked bounds) | **PASS** |
| Vacuous `fallbackCount >= 0` removed | **PASS** |

**Honesty (accepted, documented):** resolution injects `RunFact`s; does **not** run combat AI/weapons/pathing. Matches original required remediation (“validator + reward-bound checks”), not full combat sim.

### 2. MAJOR — Extraction hold not continuous

| Check | Status |
|---|---|
| `AdvanceExtraction` resets if `!(zone && interact)` | **PASS** |
| Regression `ReleasingInteractInZoneResetsExtractionAndRejectsDiscontinuousHold` | **PASS** |
| Independent probe: half-hold → release in-zone → progress `0`; second half alone does not terminalize | **PASS** (`half=180`, `afterRelease=0`, phase stays `Extraction`) |

### 3. MAJOR — Hard `GenerateFallback` unproven / invalid

| Check | Status |
|---|---|
| Soft retry still covered (`Attempt==1`) | **PASS** |
| `HardGenerateFallbackFingerprintIsDeterministic` forces `Attempt==4`, validates, fingerprint equality (Cinder + Ion) | **PASS** |
| Hard fallback Ferrite floor fix (≥15 Ferrite cells) | **PASS** (`forcedHard: Valid=True`) |

---

## Re-executed evidence (this recheck)

### `scripts/test.ps1` @ `9f30f7e`

```text
Exit: 0
Build: 0 Warning(s) / 0 Error(s)

ShipGame.Ecs.Tests:            10 passed
ShipGame.Telemetry.Tests:       7 passed
ShipGame.Persistence.Tests:    14 passed
ShipGame.Content.Tests:         8 passed
ShipGame.Game.Smoke.Tests:      2 passed
ShipGame.Architecture.Tests:    8 passed
ShipGame.Simulation.Tests:     31 passed
Total:                         80 passed, 0 failed
```

### `P3WorldRunTests` @ `9f30f7e`

```text
Exit: 0
Total tests: 19
Passed: 19

TenThousandSeedsPerEnvironmentSatisfyWorldRunInvariants(ENV_CINDER_BELT): ~1 s
TenThousandSeedsPerEnvironmentSatisfyWorldRunInvariants(ENV_ION_VEIL):     ~1 s
HardGenerateFallbackFingerprintIsDeterministic: PASS
SoftRetryFallbackFingerprintIsDeterministic: PASS
ReleasingInteractInZoneResetsExtractionAndRejectsDiscontinuousHold: PASS
```

### Seed / fallback counts (authoritative Theory + independent probe)

| Environment | Seeds validated | softOrHardFallback | hard Attempt≥4 |
|---|---:|---:|---:|
| `ENV_CINDER_BELT` | **10,000 / 10,000** | 0 | 0 |
| `ENV_ION_VEIL` | **10,000 / 10,000** | 0 | 0 |
| **Combined** | **20,000 / 20,000** | 0 | 0 |

GenerationVersion: **1**  
Natural hard fallback still absent in 20k; forced path covered and valid.

---

## Residual notes (non-blocking)

- Per-seed reward path injects fixed 30 Ferrite / 1 Data Core; descriptor Ferrite floor still proves objective feasibility from layout. Acceptable under stated honesty policy.
- `candidate-commit.txt` still names pre-remediation `6d0f131` (docs hygiene only).
- Prior minors (golden corpus, spawn anchors) were outside required remediation minimum; unchanged.

---

## Verdict

**ACCEPT**