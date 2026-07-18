# P4_META_UI Extension Note / Contract-Change Proposal

## Motivation

Foundation `ProfileSnapshot` only stores `ProfileSeed` and `RunIndex`. Meta progression requires balances, lifetime counters, research, environments, loadout, transaction receipts, settings, and previous-run summary without widening the P0 walking-skeleton save mid-flight for parallel packages.

## Old / new shape

| Concern | Foundation (unchanged) | P4-owned |
| --- | --- | --- |
| Profile | `ProfileSnapshot` | `MetaProfileSnapshot` |
| Rewards | n/a | `RewardProposal` |
| Save file | `profile.json` schema 1 | `profile-v2.json` schema 2 |
| Continue | `SaveRepository` | `MetaSaveRepository` / `MetaSession` |

## Impacted packages

- `P4_META_UI` (owner): Domain profile contracts, Simulation progression, Persistence meta saves, Telemetry translations, Game MetaUi/MetaSession.
- `P5_INTEGRATION` (consumer): should compose MetaSession into the host and decide whether to retire dual save files.
- P1/P2/P3: consume `RewardProposal` / capability queries only through accepted contracts; do not inspect research IDs for travel.

## Proposed later integration into FoundationContracts

1. Promote `MetaProfileSnapshot` (or a trimmed durable subset) into shared Domain contracts owned by integration.
2. Collapse dual repositories behind one facade with migrations from schema 1→2 already implemented in `MetaSaveRepository.MigrateFoundation`.
3. Keep capability queries semantic (`CAP_*`); never gate travel on `RES_*` IDs.

## Compatibility tests already covering the bridge

- Foundation schema 1 → meta schema 2 migration.
- Unknown research/module IDs preserved with diagnostics.
- Newer save schema rejected as `IncompatibleNewer`.
