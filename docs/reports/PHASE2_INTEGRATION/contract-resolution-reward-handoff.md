# Contract resolution: RewardProposal handoff

## Conflict

After merging P3 and P4, `ShipGame.Simulation` contained two `RewardProposal` / `ResourceAmounts` types:
- P3 `WorldRun` local run-accounting types (int-based held/banked/lost)
- P4 Domain `RewardProposal` / `ResourceAmounts` (long-based commit DTO)

Unqualified names in `MetaProgression` bound to the P3 Simulation types and broke the build.

## Resolution (ownership)

- P3 retains calculation ownership under renamed types `WorldRewardProposal` / `WorldResourceAmounts`.
- P4 retains commit ownership of Domain `RewardProposal` / `ResourceAmounts`.
- Integration adds `RewardHandoff.ToProfileProposal` to map P3 proposals into the P4 DTO without duplicating banking rules.
- No silent duplicate of research/loadout/combat rules.

## Version impact

- Save/content/generation/RNG/replay: unchanged (additive mapping only).
- Telemetry: unchanged.
