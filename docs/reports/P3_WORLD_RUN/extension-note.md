# P3_WORLD_RUN Extension Note

## Data-only additions

- New environment IDs can reuse hazard schedule parameters and resource-cell weight tables if they fit existing `HazardDescriptor` / cell-kind behavior.
- Additional upgrade definitions require catalog uniqueness (still twelve for MVP offer math) or a reviewed pool-size change.
- Presentation cue asset IDs can be rebound in `WorldRunPresentationBindings` without simulation changes when semantics are unchanged.

## Additions requiring focused code

- New hazard behaviors beyond directional flare / areal ion circles need new resolve rules and tests.
- Enemy spawners, weapon/mining contact producers, and profile banking must be wired by owning packages using P3 facts/events/proposals.
- Registering P3 systems on the authoritative scheduler requires P0/P5 schedule ownership review.
- Changing seed derivation, layout algorithm, or loot tables requires a generation and/or RNG version bump plus a full 10k×environments sweep.

## Stable contracts established locally by P3

- `GenerationIdentity` + immutable `FieldDescriptor` with flood-fill validation and deterministic fallback.
- Named stream usage: Layout (generation), Loot (drops), Upgrade (offers).
- `RunFact` ingress, ordered `WorldRunEvent` egress, exactly-once `RewardProposal`.
- Upgrade thresholds `[30, 75, 135, 210]` and twelve-ID non-repeating offer pool.
- Terminal priority: hull death > completed extraction > deadline.

## Version and migration obligations

- Layout/loot/offer algorithm changes that alter authoritative output: increment generation and/or RNG versions; regenerate goldens; re-run 20,000-seed sweep.
- Promoting local types into shared Domain/Persistence contracts: follow `contract-change-proposal.md` accept path before replay/save depend on them.
- Reward banking semantics changes affect P4 profile transactions and may require save/telemetry review.
