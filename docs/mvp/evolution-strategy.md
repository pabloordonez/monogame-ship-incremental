# Evolution Strategy

## Intent

Grow the proven loop through stable data contracts, narrow interfaces, explicit versions, and composable systems. Future readiness means a clear route for known features, not prebuilding them.

## Change rules

1. Preserve inward dependency direction and keep MonoGame at the host/presentation boundary.
2. Add component data and focused systems before inheritance hierarchies.
3. Extend the explicit schedule; do not add hidden callbacks.
4. Use closed behavior registries keyed by stable IDs, not reflection or runtime type names.
5. Query semantic capabilities, not particular research IDs.
6. Version changes that alter persisted meaning, seeded output, replay, or telemetry semantics.
7. Add migrations before releasing breaking persistent changes.
8. Update golden fixtures and version IDs for intentional deterministic changes.
9. Remove unused extension points rather than maintaining hypothetical flexibility.
10. Introduce plugins only if external modding becomes a defined product requirement.

## Standard extension seams

### Content catalog

New values, relationships, enemies, modules, upgrades, research, environments, drops, and encounters should normally be definitions using existing behavior keys. A new behavior key requires a focused implementation, validator, and deterministic test.

### Capabilities and modifiers

Research/equipment grant semantic values such as `CAP_TRAVEL_ION_VEIL`, `CAP_MINING_TIER_2`, or `CAP_PLANET_PROBE`. Consumers never inspect the granting research node.

### Commands and events

Controls enter as versioned framework-neutral commands. Feedback, UI, and telemetry observe immutable events. Systems do not call screens or audio.

### Generation pipeline

Versioned stages own topology/template selection, environment rules, sectors, entity placement, rewards/objectives, and validation. New stages receive stable inputs and dedicated RNG streams. Released generation versions stay reproducible.

### Effect resolution

Combat/mining/abilities can share only proven effect descriptors: damage, restore, impulse, extraction, status, spawn, or capability grant. Do not build a scripting or node-graph engine.

### Persistence

Each persistent subsystem contributes explicit DTOs to the profile snapshot builder and owns migrations. Raw ECS stores are never persisted.

### Presentation binding

Definitions map gameplay concepts to asset IDs. Sprites, sounds, particles, shaders, and generated art may change without changing simulation.

## Deferred-feature map

### Additional weapons and specialization

- **Use:** behavior registry, projectile/beam/guidance components, target strategy, effect resolution, modifier aggregation.
- **Likely change:** add focused plasma, spread, shield-bypass, or status systems at explicit combat positions.
- **Avoid now:** weapon scripting language.

### Advanced boost and light-speed attacks

- **Use:** command model, capability grants, cooldown, mobility, swept collision, endpoint effects.
- **Likely change:** add an ability component/system and state its order before normal collisions.

### Companion ships and fleets

- **Use:** ordinary entities with owner, formation/steering, AI commands, and normal weapons.
- **Likely change:** persistent roster/loadout DTOs if companions survive runs.
- **Avoid now:** companion class hierarchy.

### Fuel and expedition range

- **Use:** profile resources, travel validation, resolved statistics, atomic transactions.
- **Likely change:** add fuel state/cost and a save migration.

### Trading

- **Use:** inventory/profile transactions, location modifiers, deterministic economic snapshots.
- **Likely change:** quote/commit commands with idempotent transaction IDs.

### Alien races and reputation

- **Use:** stable faction IDs, profile relationship records, generation constraints, capability/reward grants.
- **Likely change:** profile schema and encounter policies; presentation supplies dialogue.

### Planet orbit, scanning, and probes

- **Use:** immutable generated celestial descriptors, capabilities, application states, profile discoveries.
- **Likely change:** instantiate planets as records first, entities only during a relevant scene.

### Facilities, passive production, and automation

- **Use:** a coarse deterministic economy separate from the real-time encounter ECS, recipe data, ledgers, galaxy node IDs.
- **Likely change:** bounded time-step advancement and an explicit offline-progress policy.
- **Avoid now:** continuously simulating cargo ships.

### Trade routes and route defense

- **Use:** galaxy edges, economy ledger, scheduled encounter descriptors.
- **Likely change:** route-risk calculation produces normal encounter content.

### Advanced galaxy access

- **Use:** destination tags/requirements, capability set, hazard effects, generation difficulty stages.
- **Likely change:** mostly data for heat/gravity gates; focused systems for black-hole or gravity behavior.

### General destructible terrain

- **Use:** collider provider, deterministic geometry commands, resource-node state, presentation extraction.
- **Prerequisite:** choose and benchmark a bounded representation such as cells or convex chunks.
- **Likely change:** generation/replay/save version impact if destruction persists.
- **Avoid now:** generic physics-plugin seam.

### Procedural stars, planets, moons, and nebulae

- **Use:** immutable descriptors, versioned generation stages, authoritative and cosmetic RNG separation.
- **Likely change:** resource facts remain authoritative; high-detail visuals use cosmetic seeds.

### Bloom, lighting, and pixel shadows

- **Use:** render passes, presentation bindings, camera state, cosmetic RNG.
- **Likely change:** none in simulation unless an effect represents a real hazard.

### Larger research trees and exclusive branches

- **Use:** graph validation, capability grants, modifier precedence, loadout constraints.
- **Prerequisite:** specify stacking and refund/migration policy before introducing exclusivity.

### Narrative and introductions

- **Use:** application state flow, content IDs, profile flags, commands/events.
- **Likely change:** small explicit sequence definitions; scripting only after repeated scenes prove a need.

### Cross-platform releases and cloud saves

- **Use:** existing platform-neutral simulation, path/storage adapters, compatibility results.
- **Prerequisite:** test numeric/replay guarantees per target; define cloud conflict resolution and identity/security.

## Version decision guide

- Increment **save** when persisted shape or meaning changes.
- Increment **content schema** when definition structure/interpretation changes.
- Change **catalog fingerprint** when shipped definitions/assets change compatibly or incompatibly.
- Increment **generation** when identical identity could produce different authoritative output.
- Increment **RNG** when algorithm/seed derivation changes.
- Increment **replay** when commands, initial state, tick rules, or hashes change.
- Increment **telemetry** when event meaning, required fields, or units change.

## Migration workflow

1. Preserve the old DTO type.
2. Define the new DTO.
3. Implement one migration from the immediately previous version.
4. Register it in the contiguous pipeline.
5. Add golden input/output fixtures.
6. Validate content IDs and generation compatibility.
7. Preserve the original save as backup.
8. Write the migrated save atomically.
9. Emit a diagnostic; never partially load.

Prefer keeping old generation implementations over regenerating worlds. Retire one only through a tested conversion and documented support decision.

## New-feature checklist

Every feature proposal states:

- Player problem and measurable hypothesis.
- Authoritative state and owner.
- Components/profile DTOs.
- Commands/events and exact scheduler position.
- RNG stream.
- IDs and validation.
- Save/content/generation/replay/telemetry impact.
- Migration and deterministic tests.
- Presentation assets.
- Explicit non-responsibilities and removal criteria.

## Representative extensibility checks

Before foundation acceptance, walk through adding:

- A fourth weapon via one behavior registration, definitions, assets, and tests.
- A fourth enemy via AI/content composition without combat rewrites.
- A star hazard through environment data and one focused system.
- Reputation through a new profile subsystem consumed by generation.
- A planet facility through immutable galaxy IDs and a separate economy.
- An automation chain through recipes/ledgers rather than encounter entities.

If an addition requires unrelated project edits, record whether the boundary is wrong or whether the feature genuinely introduces a new layer. Refactor only when that layer is approved.
