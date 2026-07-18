# P4_META_UI Independent Reviewer Report

## Identity

- Package: `P4_META_UI`
- Worktree: `C:\Repositories\github\ship-game-p4`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Candidate implementation: `61e1c80e43fb5964ba03dee736f7a38de8ef76ae`
- Review range: `0b12902972d5a98ff785c78a9e0c10728b2a2df0..61e1c80e43fb5964ba03dee736f7a38de8ef76ae`
- Package spec: `docs/reports/P4_META_UI/package-spec.md`
- Reviewer made no tracked edits; no files were written under `docs/reports/P4_META_UI/`.

## Diff scope / ownership

Changed paths (all adds):

- `docs/reports/P4_META_UI/package-spec.md`
- `src/ShipGame.Domain/ProfileContracts.cs`
- `src/ShipGame.Game/MetaUi.cs`
- `src/ShipGame.Persistence/MetaPersistence.cs`
- `src/ShipGame.Simulation/MetaProgression.cs`
- `src/ShipGame.Telemetry/MetaTelemetry.cs`
- `tests/ShipGame.Game.Smoke.Tests/MetaUiTests.cs`
- `tests/ShipGame.Persistence.Tests/MetaPersistenceTests.cs`
- `tests/ShipGame.Simulation.Tests/MetaProgressionTests.cs`
- `tests/ShipGame.Telemetry.Tests/MetaTelemetryTests.cs`

No modified P0/P1/P2/P3 shared paths. Ownership of new Domain/Simulation/Persistence/Telemetry/Game/test files is clean. Candidate commit does not contain implementer evidence artifacts required by the package Evidence section.

## Command evidence (re-run)

| Command | Result |
|---|---|
| `scripts/test.ps1` | exit 0; build 0 warnings / 0 errors |
| Full suite (`dotnet test` Release) | 86 passed, 0 failed (Arch 8, Smoke 5, Ecs 10, Content 8, Persistence 22, Simulation 22, Telemetry 11) |
| Meta filters | MetaProgression 10/10, MetaPersistence 8/8, MetaTelemetry 4/4, MetaUi 4/4 |
| `scripts/launch.ps1 -Smoke` | exit 0 |
| `scripts/launch.ps1 -WindowSmoke` | exit 0; `DESKTOPVK_CONTENT_READY`, `DESKTOPVK_WALKING_SKELETON_COMPLETE` |

These green exits do **not** establish acceptance. Adversarial checks below falsify continue/migration/durability gates.

## Gate-by-gate verdict

| Gate | Verdict | Evidence |
|---|---|---|
| New profile meta loop (research, loadout, save, continue) | **Partial** | `MetaUiTests.MetaLoop...` passes against `MetaSession`. Host (`ShipGameHost`) still uses foundation `ProfileSnapshot` and does not compose `MetaSession`. |
| Twelve nodes acyclic/reachable; catalog costs | **Pass** | `ResearchCatalog` has exactly 12 nodes matching `content-catalog.md`; costs sum 565/14/15; `ValidateGraph()` empty; purchase-all path in tests + harness. |
| Ion Veil capability (not research ID) | **Pass** | `ValidateDestination` / map lock use `HasCapability(CAP_TRAVEL_ION_VEIL)`. Harness: unlocked-env list alone rejected; `CAP_*` injected into purchased IDs rejected; research ID is not a capability. |
| Transactions atomic/idempotent; nonnegative balances | **Pass** | Reward/research/equip conflict/duplicate/overflow/unbalanced rejection covered; unaffordable purchase leaves snapshot unchanged. |
| Schema 2 + foundation migration | **Fail** | `MetaSaveSchema.Current = 2` and schema-1→2 transform exist, but only when schema-1 bytes live at `profile-v2.json`. Foundation `SaveRepository` writes `profile.json`; `MetaSession` ignores it and creates a new profile (identity loss). |
| Golden/corrupt/newer/unknown/interrupted saves | **Fail** | Corrupt/newer/unknown/interrupted covered. No golden old-save fixture. Corrupt primary+backup causes silent new-profile reset in `MetaSession`. |
| Telemetry consent/disable/failure/privacy; cannot affect play | **Pass (narrow)** | Consent off, revoke, sink failure isolation, schema event names, no email/name/raw keys. Play continues after telemetry failure. |
| Package/arch/full/headless/graphical smoke with exact evidence | **Fail** | Suites/smokes green under this review, but candidate SHA has no `implementer-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, or `manual-evidence/`. |

## Adversarial falsifications

### Research graph
- Twelve unique IDs, catalog-aligned costs/deps/gates/grants, topo-sort visits all nodes.
- `RES_RECOVERY_PROTOCOLS` gate (Ion Veil extraction) is not a graph cycle; reachable after Nav Ion path + extraction counter.

### Ion Veil
- Access is capability-based (`CAP_TRAVEL_ION_VEIL` grant lookup).
- Not unlocked by `UnlockedEnvironmentIds` alone, research-ID capability query, or stuffing `CAP_TRAVEL_ION_VEIL` into purchased research.

### Transactions
- Duplicate same fingerprint → `Duplicate`; conflict reuse → reject without mutation; same run twice → `reward.run-already-committed`; balances stay nonnegative.

### Saves / migrations (broken)
1. **Foundation path gap:** with only `profile.json` (schema 1), `MetaSession` reports a new profile (`LOST_FOUNDATION=True`); seed/runIndex from foundation continue are discarded.
2. **Silent reset:** corrupt `profile-v2.json` + `.bak` → `MetaSession` constructs `CreateNew` (`SILENT_RESET=True`), violating “no silent reset.”
3. **Accepted mutation, failed persist:** forcing write failure after purchase still returns `Accepted=True` with in-memory research; reopen yields empty profile / lost balances.

### Telemetry privacy
- Facts use codes/amounts/booleans; default consent false; failures contained. Privacy test is shallow (key-name checks only); `string.GetHashCode()` subject codes are local/runtime-unstable but not raw content strings.

### Ownership
- Diff is additive under owned layers; `ContractVersions.Save` left at 1 with parallel `MetaSaveSchema.Current = 2` (commented). Missing contract/extension note on candidate SHA.

## Findings

### M1 — MAJOR: foundation `profile.json` is never migrated on continue

`SaveRepository` defaults to `profile.json`; `MetaSaveRepository`/`MetaSession` default to `profile-v2.json`. Schema-1→2 migration only runs if foundation-shaped bytes are already at the meta filename. A real foundation save is treated as `Missing` and replaced by a new meta profile.

**Fix:** On load, discover `profile.json` (and backup), migrate into `profile-v2.json` (atomic write + backup), and add a regression that places a golden foundation file at the foundation path and asserts seed/runIndex continue.

### M2 — MAJOR: corrupt/unreadable meta saves silently reset the profile

When load is not `Supported`, `MetaSession` always `CreateNew`. Dual-corrupt primary/backup wipes progress without surfacing unrecoverable state to the UI/API consumer.

**Fix:** Distinguish Missing vs Corrupt/Incompatible; refuse silent overwrite of corrupt durable state; require explicit new-profile action; regression for dual-corrupt.

### M3 — MAJOR: successful meta mutations can return Accepted while durability fails

`MetaSession.PurchaseResearch` / `EquipModule` / `CommitReward` / settings mutate memory then call `Persist`, which swallows IO failures and still returns the mutation success. Reproduced: purchase `Accepted=True`, continue has 0 research and zero balances.

**Fix:** Surface persist failure on the mutation/UI result (or roll back / quarantine); do not report durable success when write/verify fails; add regression.

### M4 — MAJOR: golden old-save gate unmet

Persistence tests cover round-trip, synthetic foundation-at-`profile-v2`, corrupt backup recovery, newer, unknown IDs, interrupted temp. No checked-in golden schema-1 (or prior) fixture exercising the real upgrade path. systems.md requires golden old saves + sequential migrations.

**Fix:** Add golden `profile.json` fixture + migration assertion to schema 2 with preserved identity defaults.

### M5 — MAJOR: candidate lacks required package evidence

At `61e1c80`, `docs/reports/P4_META_UI/` contains only `package-spec.md`. ACCEPT requires evidence for every gate (`implementer-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, `manual-evidence/`, etc.).

**Fix:** Publish exact command transcripts, candidate SHA pin, extension/contract note (especially dual save schema), and smoke notes on the candidate evidence commit.

### m1 — MINOR: meta UI not composed into `ShipGameHost`

Screen model/`MetaSession` exist and are smoke-tested, but the DesktopVK host still runs the foundation walking skeleton only. Acceptable to defer presentation wiring to integration only if extension note says so; otherwise navigation remains unreachable in the shipped host.

### m2 — MINOR: “lifetime kills” gate uses `NormalKills` only

Catalog wording is “20 lifetime kills”; implementation ignores elite kills. Clarify catalog or count `NormalKills + EliteKills`.

### m3 — MINOR: telemetry privacy assertions are keyword-shallow

No rejection test for oversized/raw string payloads at the meta translator boundary (relies on subject codes + consent wrapper).

## Missing / misleading tests

- No test that foundation `profile.json` in the save directory continues as migrated meta.
- No dual-corrupt / MissingContent continue semantics on `MetaSession`.
- No persist-failure → non-Accepted (or rolled-back) mutation test.
- No golden schema-1 fixture.
- Ion Veil / graph / transaction tests are generally honest and matched adversarial harness results.

## Downstream impact

- Players with P0 saves lose identity when entering meta continue.
- Disk/permission failures can acknowledge research/loadout/rewards that vanish on restart.
- Corrupt saves can hard-reset progression.
- Integration (P5) cannot trust meta continue/migration without remediating the dual-file and silent-reset paths.

## Verdict

REMEDIATE