# MVP Game Design

## State flow

`Boot -> Title -> Lobby -> Loadout/Research/Map -> Run -> Summary -> Lobby`

Continue loads the latest valid profile. New Game creates a seeded profile and equips all default modules. Options and pause expose volume, vibration, screen shake, flashes, and input remapping where supported.

## Wayfarer Mk I

Base statistics before equipment and research:

- Hull: 100.
- Collision radius: 18 world units.
- Pickup radius: 70 units.
- Five slots: weapon, mining, shield, engine, and utility.

Shield absorbs damage before hull; excess spills into hull. Zero hull fails the run. After hull damage, the ship receives 0.35 seconds of projectile/collision invulnerability.

## Controls

Keyboard and mouse:

- `W/A/S/D`: screen-relative thrust.
- Mouse: aim.
- Left mouse: weapon.
- Right mouse: mining tool.
- `Space`: dash or blink.
- `E`: interact or hold to extract.
- `1/2/3`: select an upgrade.
- `Tab`: objective and held resources.
- `Esc`: pause.

Gamepad:

- Left stick: thrust.
- Right stick: aim; retain the last non-zero direction.
- Right/left trigger: weapon/mining.
- Left bumper: dash or blink.
- `X`: interact or hold to extract.
- D-pad and `A`: select an upgrade.
- View/Menu: objective/pause.

Weapon and mining tools cannot operate simultaneously; weapon input wins. Controller aim may snap within 12 degrees toward a target no farther than 500 units. Menus and upgrade choices pause the single-player simulation.

## Movement and feedback

- Movement uses acceleration and responsive braking rather than permanent drift.
- The engine module defines speed and mobility action.
- Contact is harmless unless an entity owns an explicit contact-damage component.
- Every hostile attack has a pre-fire or area telegraph.
- Damage, shield break, mining, collection, objective completion, and extraction use distinct visual and audio cues.
- Screen shake and flashes are limited and optional.

## Seeded field

Each generated run contains:

- One safe spawn clearing.
- Three objective sectors.
- One reserved elite arena.
- One extraction sector at least 700 units from spawn.
- Traversable corridors connecting all required sectors.
- Asteroid clusters, resource cells, enemy anchors, and environment hazards.

A validator flood-fills player-clearance space and verifies objective, elite, and extraction reachability. Invalid output regenerates using a deterministic fallback sub-seed. Asteroids use bounded cells/chunks; the MVP does not implement arbitrary terrain deformation.

## Run sequence

### Launch

The environment, equipped modules, research modifiers, content version, generation version, and run seed are locked. The run index is persisted before field entry.

### Objective

`OBJ_FIELD_PROOF` requires both:

- Collect 30 Ferrite in the current run.
- Destroy eight normal enemies.

Progress can occur in any order. Collected objective Ferrite remains loot.

When both are complete, normal spawning pauses briefly, the elite arena is marked, and one normal archetype receives `MOD_ELITE_PROTOCOL`. Its defeat drops one Data Core and activates extraction.

### Extraction

`EXT_STANDARD_GATE` becomes visible on the HUD and field-edge indicator.

- Enter the zone and hold interact for six continuous seconds.
- Damage does not interrupt progress.
- Leaving resets progress.
- Enemies spawn at maximum environment threat during the hold.
- Completion immediately resolves a successful run.

### Timer and failure

- Timer starts when the ship spawns.
- At 10:00, a two-minute collapse warning begins.
- At 12:00, the run fails unless extraction is complete.
- Zero hull also fails the run.
- Pause and upgrade selection do not advance time.

## Threat progression

- Before `3:00`: at most four normal enemies.
- From `3:00` until objective completion: at most six; mixed archetypes enabled.
- At `6:00`, the pre-objective cap rises to eight if the objective is still incomplete.
- From objective completion through elite defeat: at most eight normal enemies plus the elite.
- From extraction activation through run end: at most ten normal enemies.

Spawn anchors must be at least 450 units away and outside the camera. Composition and timing use the encounter RNG stream.

## Combat

- Pulse Cannon fires discrete projectiles.
- Beam Emitter applies continuous precision damage and heat.
- Seeker Rack launches slower homing missiles with a target lock.
- Shields recharge only after their no-damage delay.
- Simultaneous damage resolves in stable source/entity order.
- Player weapons apply 20% listed damage to asteroid cells unless a module says otherwise.

## Mining and collection

Asteroids contain ordinary and resource-bearing cells. Mining damage reduces cell health; breaking a cell releases pickups according to the environment table. The mining tool applies full mining damage and cannot hurt the player.

Pickups within collection radius accelerate toward the ship and are credited exactly once. The utility module changes collection behavior. There is no cargo-capacity limit in the MVP; collection should encourage continued play, not inventory management.

## Temporary upgrades

Upgrade charge is not a persistent resource:

- Break a resource-bearing cell: 3 charge.
- Destroy a normal enemy: 8 charge.
- Destroy the elite: 20 charge.

At cumulative charge 30, 75, 135, and 210:

1. Pause simulation.
2. Offer three distinct, unowned upgrades from the catalog.
3. Let the player choose one.
4. Resume after applying it.

Upgrades cannot repeat, rank, or reroll in the MVP. Multiple crossed thresholds resolve consecutively. All are removed at run end.

## Resources and run resolution

- `MAT_FERRITE`: common construction material.
- `MAT_LUMEN`: uncommon crystal from resource cells.
- `MAT_DATA_CORE`: elite research artifact.

Success banks all held resources. Failure retains 25% of held Ferrite, rounded down, and loses held Lumen/Data Cores. `RES_RECOVERY_PROTOCOLS` raises Ferrite retention to 50%. Banked resources and purchased research are never lost.

The summary separates earned, banked, retained, and lost amounts and shows progress toward affordable research.

## Lobby rules

The lobby exposes:

- Both environments and their access requirements.
- Five-slot loadout editor with measured stat changes.
- Resource balances.
- Research graph, costs, prerequisites, and capability rewards.
- Previous-run result and launch action.

Only unlocked modules can be equipped. Unknown or incompatible saved module IDs fall back to the slot default with a visible diagnostic; the original save is preserved for recovery.

## Save contract

Persist at least:

- Save/content/generation/RNG schema versions.
- Profile seed and run index.
- Three banked resource balances.
- Purchased research IDs and unlocked environment IDs.
- Equipped module ID by slot.
- Lifetime counters used by research gates.
- Settings and telemetry consent.

Purchased research is never silently removed. Renamed IDs require explicit migrations.
