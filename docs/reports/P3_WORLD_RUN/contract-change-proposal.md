# P3_WORLD_RUN Contract Change Proposal

Status: proposal only (no shared-contract edits in this package).

## Motivation

P3 delivers a self-contained world-run simulation (`EncounterGenerator`, `EncounterValidator`, mining/loot/collection systems, `RunUpgradeSystem`, `WorldRunSimulation`, presentation bindings) without mutating P0 `FoundationSimulation` / `FoundationContracts` / host shell. Downstream packages need explicit handoff points that are not yet owned shared contracts.

## Proposed additions (owned by named packages on accept)

### 1. Run fact / reward handoff (`P0` schedule owner + `P4` progression)

| Shape | Notes |
|---|---|
| `RunFact` / `WorldRunEvent` / `RewardProposal` | Authoritative run observations and exactly-once reward proposal. |
| Consumer | P4 commits banked amounts into profile transactions; P3 must not mutate profile balances. |

**Version impact:** none until moved into shared Domain/Persistence contracts. Replay/telemetry meaning freezes when adopted.

**Compatibility tests:** exactly-once proposal; success banks all held; failure retains 25%/50% Ferrite.

### 2. Scheduler slots (`P0` / `P5`)

Documented order in `docs/mvp/systems.md` steps 7–11 should register P3 systems (`MiningSystem`, hazard resolve, upgrades/objective/extraction, terminal reward) without P3 editing `FoundationSimulation` in this package.

**Version impact:** schedule identity / replay hash if slot order changes after integration.

### 3. Collision / destruction contacts (`P1`/`P2` owners)

`MiningContact` is the P3 ingress for cell damage. Weapon/mining contact production remains outside P3 ownership.

**Version impact:** none for P3-local structs until promoted.

### 4. Encounter spawn / elite activation (`P2` combat owner)

`EliteActivationRequested` and threat caps are events/state for combat spawners. P3 does not spawn enemies.

**Version impact:** content/generation only if elite arena descriptors change meaning.

## Impacted packages

- `P0_FOUNDATION` — schedule registration surface
- `P1_SHIP_FEEL` / `P2_COMBAT_ARENA` — contacts, hull death facts, elite spawn
- `P4_PROGRESSION_META` — reward commit, recovery protocols flag input
- `P5_INTEGRATION` — wire presentation bindings and full loop

## Migration

No save/content/generation/RNG version bump in this package. Generation version remains `1` (`EncounterGenerator.CurrentGenerationVersion` / `ContractVersions.Generation`). Promoting local types into shared contracts later requires an explicit accept + version review before replay goldens depend on them.

## Compatibility tests required on accept

- Headless same-seed event order across host wiring
- Reward proposal consumed once by P4
- Pause/upgrade clock freeze preserved after schedule merge
