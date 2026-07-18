# P4_META_UI Remediation Report

## Status

`READY_FOR_RECHECK`

- Reviewer verdict remediated: `REMEDIATE` → majors M1–M5 addressed
- Worktree: `C:\Repositories\github\ship-game-p4`
- Branch: `phase2-p4-meta-ui`
- Reviewer candidate: `61e1c80e43fb5964ba03dee736f7a38de8ef76ae`
- Reviewer report: `5324dffb96bbc6622a4072bd76a54faa77570026`
- Remediation commit: `b398b26f23dd3dfdf24a80ceeea2dca6c2517242`

## Major fixes

### M1 — Foundation `profile.json` discovery + atomic migration

`MetaSaveRepository.Load` now:

1. Loads `profile-v2.json` (+ `.bak`) when present.
2. If meta files are absent, discovers foundation `profile.json` (+ `.bak`).
3. Migrates schema 1 → 2 and writes `profile-v2.json` atomically (temp/replace/backup), preserving `ProfileSeed` / `RunIndex`.

Regression: `FoundationProfileJsonPathMigratesAtomicallyPreservingSeedAndRunIndex`, `FoundationProfileJsonContinuesWithMigratedSeedAndRunIndex`.

### M2 — No silent reset on corrupt/unreadable meta saves

`MetaSession` distinguishes:

| Load status | Behavior |
| --- | --- |
| `Missing` | `CreateNew` (first-run) |
| `Supported` | Continue |
| `Corrupt` / `IncompatibleNewer` / `MissingContent` | `RequiresExplicitNewProfile`; mutations/persist blocked; durable bytes untouched until `CreateNewProfile` |

Regression: `DualCorruptMetaSavesRequireExplicitNewProfileWithoutSilentReset`.

### M3 — Persist failure is not Accepted

Mutating APIs (`PurchaseResearch`, `EquipModule`, `ApplySettings`, `CommitReward`) call `TryPersist`; on failure they restore prior `ProfileAggregate` state and return `save.failed` / `Rejected` (not Accepted/Applied).

Regression: `PersistFailureRejectsMutationAndRollsBackInMemoryState`.

### M4 — Golden old-save fixture

Checked-in fixture: `tests/ShipGame.Persistence.Tests/Fixtures/golden-foundation-profile.json` (schema 1, seed `12648430`, runIndex `7`).

Regression: `GoldenFoundationFixtureAtProfileJsonMigratesToSchema2` asserts schema 2 durable write + identity defaults.

### M5 — Evidence artifacts

Present on branch history (`150d583`) and retained/updated on this remediation commit:

- `implementer-report.md`
- `commands-and-results.txt` (updated with remediation runs)
- `candidate-commit.txt` (original implementation pin unchanged)
- `extension-note.md` (updated)
- `manual-evidence/README.md`
- `remediation-report.md` (this file)

## Minors

- **m1:** Extension note documents `ShipGameHost` composition deferral to `P5_INTEGRATION`.
- **m2:** `RES_WEAPON_SEEKER` gate counts `NormalKills + EliteKills`; clarified in extension note + `SeekerGateCountsNormalPlusEliteLifetimeKills`.

## Command evidence (remediation)

| Command | Result |
| --- | --- |
| `scripts/test.ps1` | exit 0; 0 warnings / 0 errors |
| Full suite | **92** passed (Arch 8, Smoke 8, Ecs 10, Content 8, Persistence 24, Simulation 23, Telemetry 11) |
| Meta filters | Persistence Meta 10, Simulation Meta 11, Telemetry Meta 4, Game MetaUi 7 |

Exact transcripts appended in `commands-and-results.txt`.
