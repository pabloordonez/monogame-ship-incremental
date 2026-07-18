# P3_WORLD_RUN Independent Adversarial Reviewer Report

**Package:** `P3_WORLD_RUN`  
**Worktree:** `C:\Repositories\github\ship-game-p3`  
**Pinned base:** `0b12902972d5a98ff785c78a9e0c10728b2a2df0`  
**Candidate:** `6d0f131915263f6f183788e6107617e536519788`  
**Reviewer stance:** independent, read-only; existing `reviewer-report.md` ignored and not written  
**Date:** 2026-07-18  

---

## Diff inspection (`0b12902..6d0f131`)

**Implementation files (7):** +1673 LOC production/tests

| Path | Role |
|---|---|
| `src/ShipGame.Simulation/WorldGeneration.cs` | Descriptor, generator, flood-fill validator, hazards |
| `src/ShipGame.Simulation/WorldMining.cs` | Mining / loot / collection |
| `src/ShipGame.Simulation/WorldRun.cs` | Run clock, objective, elite, extraction, reward proposal |
| `src/ShipGame.Simulation/RunUpgrades.cs` | Charge, 12-catalog offers, temporary modifiers |
| `src/ShipGame.Game/WorldRunPresentation.cs` | Event → presentation cue bindings |
| `tests/ShipGame.Simulation.Tests/P3WorldRunTests.cs` | Package gates |
| `tests/ShipGame.Game.Smoke.Tests/P3WorldRunPresentationTests.cs` | Binding smoke |

**Also in range:** package evidence docs + `contract-change-proposal.md` (spec freeze `7d1b359` then implementation).

**Ownership / scope:** No edits to `FoundationSimulation`, `FoundationContracts`, `ContentContracts`, `ShipGameHost`, Persistence, or P1/P2/P4 owned paths. Local handoff types + proposal only. **Pass.**

**Profile banking:** `RewardProposal` computes `Banked`/`Lost` amounts only. No `ProfileSnapshot`, save write, or balance mutation in P3 code. **Pass.**

---

## Re-executed evidence (this review)

### `scripts/test.ps1`

```text
Command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test.ps1
Exit: 0
Build: 0 Warning(s) / 0 Error(s); content builder 2 succeeded / 0 failed

ShipGame.Ecs.Tests:            10 passed
ShipGame.Telemetry.Tests:       7 passed
ShipGame.Persistence.Tests:    14 passed
ShipGame.Content.Tests:         8 passed
ShipGame.Game.Smoke.Tests:      2 passed
ShipGame.Architecture.Tests:    8 passed
ShipGame.Simulation.Tests:     29 passed
Total:                         78 passed, 0 failed
```

### `P3WorldRunTests` (includes 20k seed sweep)

```text
Command: dotnet test tests/ShipGame.Simulation.Tests/ShipGame.Simulation.Tests.csproj
         --filter "FullyQualifiedName~P3WorldRunTests" --configuration Release --verbosity normal
Exit: 0
Total tests: 17
Passed: 17

TenThousandSeedsPerEnvironmentSatisfyGenerationInvariants(ENV_CINDER_BELT): 10,000 seeds, ~926 ms
TenThousandSeedsPerEnvironmentSatisfyGenerationInvariants(ENV_ION_VEIL):     10,000 seeds, ~721 ms
Combined generation sweep: 20,000 / 20,000 validated for GenerationVersion=1
```

Focused adversarial probes (outside suite):

- Natural `FallbackUsed` over 10k×2 environments: **0** (hard `Attempt>=4` path never hit).
- Forced `invalidatePrimaryForTest`: soft retry `Attempt=1` only — **`GenerateFallback` not exercised**.
- Extraction interact-release while remaining in zone: progress **preserved**; discontinuous 180+180 ticks reaches **Success**.

---

## Gate-by-gate verdict

| Gate | Verdict | Evidence |
|---|---|---|
| Scope / ownership | **PASS** | Diff limited to Simulation/Game/tests (+ package docs/proposal) |
| No profile banking in P3 | **PASS** | Proposal-only `Banked`; Persistence/Profile untouched |
| RNG stream isolation | **PASS** (weak test surface) | Layout/Loot/Upgrade streams used correctly; unit test advances Loot without shifting Layout/Upgrade |
| Mining conservation / collect-once | **PASS** | Focused test; despawn + elite Data Core once |
| Four upgrades in one run | **PASS** | Thresholds 30/75/135/210; 12 unique offered IDs across 4×3 |
| Pause / upgrade clock freeze | **PASS** | Pending offer / `Paused` do not advance `RunTick` |
| Terminal priority + exactly-once reward | **PASS** | Death > extraction > deadline; terminal steps emit no second proposal |
| Failure retention 25%/50% | **PASS** | 39→9 / 19 Ferrite; Lumen/DataCore lost on failure |
| Same seed → identical events/result | **PASS** | Ordered event fingerprint equality |
| Deterministic fallback | **PARTIAL FAIL** | Soft retry covered; hard `GenerateFallback` untested/unreachable in 20k natural seeds |
| 10k/env: traversal + objective + elite + extraction + fallback + reward | **FAIL** | Sweep only asserts generation/`EncounterValidator` invariants; name admits “GenerationInvariants” |
| Package tests + architecture + full suite | **PASS** | 78/78 |
| Headless/window smoke | **NOT RE-RUN here** | Implementer evidence cited; not required by this review prompt |

---

## Findings

### Critical

1. **10,000-seed package gate is not met (coverage laundering).**  
   Package-spec / `agent-workflow.md` require each of 10k seeds/environment to satisfy **traversal, objective, elite, extraction, fallback, and reward** invariants (20k total).  
   `TenThousandSeedsPerEnvironmentSatisfyGenerationInvariants` only checks descriptor validity (sectors, reachability via validator, Ferrite floor, hazard damage/bounds, generation version). It does **not** exercise objective completion, elite activation, extraction success, reward bounds, or fallback per seed.  
   Implementer report presents this Theory as satisfying the 20k gate. That overclaims. Green tests ≠ gate completion.

### Major

2. **Extraction hold is not continuous.**  
   Design: hold interact for six continuous seconds; leaving resets.  
   Implementation resets only when `!PlayerInExtractionZone`. Releasing interact while still in-zone freezes progress without reset.  
   **Reproduction:** hold 180 ticks → release in-zone (`progress` stays 180) → hold 180 more → `Succeeded` at 360.  
   Missing test; current pause/leave test only covers leaving the zone.

3. **Hard deterministic fallback path is unproven.**  
   `GenerateFallback` runs only after four failed attempts. Natural 10k×2 never sets `FallbackUsed`. The determinism test forces attempt 0 invalid and accepts attempt 1 (`Attempt==1`), never the hard fallback descriptor. Fallback gate language is not evidenced.

### Minor

4. Stream-isolation follow-up constructs separate `RandomStreams` instances for loot vs upgrades rather than proving isolation on one shared instance after loot draws (first half of the test is still valid).  
5. No golden seed corpus (called for in `systems.md`).  
6. Generation omits enemy spawn anchors (deferred via contract proposal to combat owners — acceptable if explicitly owned elsewhere, but incomplete vs game-design field contents).  
7. Vacuous `Assert.True(fallbackCount >= 0)` in the 10k Theory.

---

## Claim falsification summary

| Claim | Result |
|---|---|
| Generation/reachability for 20k seeds | **Holds** for validator invariants |
| Objective/elite/extraction/reward on 20k seeds | **Falsified** (not tested in sweep) |
| Deterministic fallback | **Partially falsified** (soft retry only) |
| Reward proposal exactly-once / documented amounts | **Holds** in focused tests |
| Upgrade four-offer non-repeat / pause freeze | **Holds** |
| Stream isolation | **Holds** at PRNG layer |
| Continuous extraction hold | **Falsified** |
| No profile banking in P3 | **Holds** |

---

## Downstream impact

- P4 can consume `RewardProposal` safely (no premature profile writes).  
- P5/P0 still must wire scheduler registration (acknowledged limitation).  
- Shipping extraction as-is will accept discontinuous holds until remediating reset-on-interact-release.  
- Replay/integration must not treat the current 10k Theory as reward/objective golden coverage.

---

## Required remediation (minimum)

1. Expand the 10k×2 sweep (or an equally authoritative headless sweep) to assert per-seed **objective feasibility bounds, elite/extraction reachability already implied by validator plus reward-bound checks**, and document fallback policy (retry vs hard fallback) with a test that actually hits `GenerateFallback`.  
2. Reset extraction progress when interact is released (or update design + tests if discontinuous hold is intentional — currently contradicts game-design).  
3. Replace vacuous fallback assertions; add a hard-fallback fingerprint determinism test.  
4. Re-run `scripts/test.ps1` + focused P3 suite; refresh commands evidence without weakening gates.

---

## Verdict

**REMEDIATE**

---

## Final acceptance recheck

Independent recheck verdict: **ACCEPT**
Accepted implementation/remediation SHA: `9f30f7e6bea9fc3d17f1581b09d99e569bccb485`
Full recheck body: `recheck-report.md`.
