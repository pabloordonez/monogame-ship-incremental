# Phase 2 Handoff

Pinned P0 base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`  
Integration branch: `phase2-integration`  
Final merged tip:  (branch HEAD; pin file updated in same commit)`c7ffff0c4fa9eb8fccc0cedd264c25c08a0d5ac5`.

## Accepted package commits

| Package | Accepted implementation SHA | Final reviewer verdict | Remediation summary |
|---|---|---|---|
| P1_CONTENT_ART | `7d09a96815cb86bfa9fde89b5969e1f514c105af` | ACCEPT (after BLOCK→remediation) | Restored ContentBuildPlan fail-closed; reverted ShipGameHost edits; added pixel/palette/grayscale gates; honest animation metadata; generated-root validation |
| P2_FLIGHT_COMBAT | `6c5ffc0be6103bc2d806217f9c4adc820db0f027` | ACCEPT (after REMEDIATE) | Schedule==Step binding; non-alloc command slots; pending commands in hash |
| P3_WORLD_RUN | `9f30f7e6bea9fc3d17f1581b09d99e569bccb485` | ACCEPT (after REMEDIATE) | Expanded 20k seed sweep; continuous extraction hold; hard fallback determinism |
| P4_META_UI | `b398b26f23dd3dfdf24a80ceeea2dca6c2517242` | ACCEPT (after REMEDIATE) | foundation profile.json migrate; corrupt quarantine; persist-failure not Accepted; golden old save |

Package tip commits that pin ACCEPT evidence: P1 `c3847fe`, P2 `ebac829`, P3 `8d1f03f`, P4 `57f4d75`.

## Merge order and post-merge gates

1. P1 content/catalog — merge `b1f58dd` — suite/content/smoke green  
2. P2 movement/combat — merge `9b76f13` — suite/smoke green  
3. P3 world/mining/run — merge `246afa4` — suite/smoke green (20k seed tests included)  
4. P4 rewards/progression/UI — merge `46dcac9` — build broke on RewardProposal name clash  
5. Contract resolution — `452856d` renames P3 types + `RewardHandoff` mapper  
6. Evidence — tip after handoff evidence commit

## Final full-suite results (integration tip)

See `phase2-final-gates.txt`, `final-smoke.txt`, `final-window-smoke.txt`, `final-content-rebuild.txt`.

Expected after tip verification:
- Content rebuild: 16 succeeded / 0 failed
- Tests: Architecture 8, Content 19, Ecs 10, Game.Smoke 11, Persistence 24, Simulation 65, Telemetry 11 = **148 passed / 0 failed**
- Headless smoke exit 0
- Window smoke exit 0 with DESKTOPVK markers

## Contract / version impacts

| Axis | Impact |
|---|---|
| Save | P4 parallel `MetaSaveSchema=2` / `profile-v2.json`; foundation `ContractVersions.Save=1` retained for `profile.json` with migrate-on-continue |
| Content schema | Remains 1; catalog fingerprint changes with P1 shipped definitions/assets |
| Generation | Remains 1; 20k seed sweep for gen v1 |
| RNG | Remains 1 |
| Replay | Remains 1 (FlightCombat/WorldRun not yet wired into FoundationSimulation scheduler) |
| Telemetry | Schema 1; additive meta event names |

Shared-contract resolution: `docs/reports/PHASE2_INTEGRATION/contract-resolution-reward-handoff.md`.

## Known limitations / blocked for P5

- `ShipGameHost` still runs the P0 walking skeleton; FlightCombat / WorldRun / MetaSession are additive and tested headless but not composed into the live host loop (explicitly P5).
- First-pass art remains `candidate` with waivers; human art approval deferred.
- No music files (brief only) — intentional.
- Manual 1080p performance capture / 50 golden-path reliability marathon not executed in Phase 2.
- P3 natural hard-fallback remains rare; forced path covered.

## P5_INTEGRATION readiness

**READY_WITH_LIMITATIONS**

Phase 2 packages are independently ACCEPTed and merged with green automated gates. P5 should wire accepted contracts into the host/scheduler, unify continue entry points, exercise the full fresh+continue loop without debug commands, and close presentation/performance release gates. Do not begin release hardening until that composition lands.
