# MVP Game Design

Player-facing rules for the current MVP as implemented. When this document and code disagree, update this document.

## State flow

`Boot -> Title -> Station -> (Map | Loadout | Research | Upgrades | Settings) -> Run <-> Pause -> Summary -> Station`

Continue loads the latest valid profile. New Game creates a seeded profile and equips all default modules. Settings and pause expose flashes, particles, telemetry consent, and related options where supported.

The player-facing product title is **Mine Your Own Business**. The repository and solution remain named Ship Game.

## Wayfarer Mk I

Base statistics before equipment and research:

- Hull: 100.
- Collision radius: 18 world units.
- Pickup radius: 70 units.
- Five slots: weapon, mining, shield, engine, and utility.

Shield absorbs damage before hull; excess spills into hull. Zero hull fails the run. After hull damage, the ship receives a short projectile/collision invulnerability window.

## Controls

Keyboard and mouse:

- `W/A/S/D`: thrust.
- Mouse: aim.
- Left mouse: weapon.
- Right mouse: mining tool.
- `Space`: dash or blink.
- `E`: interact (reserved; extraction progresses by presence in the zone).
- `Tab`: objective and held resources (where bound).
- `Esc`: pause.

Gamepad:

- Left stick: thrust.
- Right stick: aim; retain the last non-zero direction.
- Right/left trigger: weapon/mining.
- Left bumper: dash or blink.
- `X`: interact.
- View/Menu: objective/pause.

Weapon and mining tools cannot operate simultaneously; weapon input wins. Controller aim may snap within 12 degrees toward a target no farther than 500 units. Pause does not advance the simulation clock.

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

The environment, equipped modules, research modifiers, purchased station upgrades, content version, generation version, and run seed are locked. The run index is persisted before field entry.

### Objective

`OBJ_FIELD_PROOF` requires both:

- Collect 30 Ferrite in the current run.
- Destroy eight normal enemies.

Progress can occur in any order. Collected objective Ferrite remains loot.

When both are complete, the run enters the elite phase. One `ENM_GUNSHIP` spawns with `MOD_ELITE_PROTOCOL` applied. Its defeat drops one Data Core and activates extraction.

### Extraction

`EXT_STANDARD_GATE` becomes visible on the HUD and field-edge indicator. Charge progress is shown as a pulsing ring and arc at the gate marker (no numeric tick HUD).

- Enter the extraction zone and remain inside for six continuous seconds.
- Leaving the zone resets progress.
- Damage does not interrupt progress while the ship remains in the zone.
- Enemies spawn at maximum threat during extraction.
- Completion immediately resolves a successful run.

### Timer and failure

- Timer starts when the ship spawns.
- At 10:00, a two-minute collapse warning begins.
- At 12:00, the run fails unless extraction is complete.
- Zero hull also fails the run.
- Pause does not advance time.

## Threat progression

- Before `3:00`: at most four normal enemies.
- From `3:00` until later caps: at most six; mixed archetypes enabled.
- At `6:00`, and through the elite phase: at most eight normal enemies (plus the elite while active).
- From extraction activation through run end: at most ten normal enemies.

Spawn anchors must be at least 450 units away and outside the camera. Composition and timing use the encounter RNG stream.

## Combat

- Pulse Cannon fires discrete projectiles (10 damage, 5 shots/second, range 650).
- Beam Emitter applies continuous hitscan damage at 100 DPS, range 600, with heat lockout after sustained fire.
- Seeker Rack launches two missiles (16 damage each) every 0.6 seconds; they home inside a 35° aim cone, otherwise fly straight.
- Shields recharge only after their no-damage delay.
- Simultaneous damage resolves in stable source/entity order.
- Player weapons apply a reduced share of listed damage to asteroid cells unless a module says otherwise.

## Mining and collection

Asteroids contain ordinary and resource-bearing cells. Mining damage reduces cell health; breaking a cell releases pickups according to the environment and loot rules. The mining laser range in the composed run is 130 world units. The seismic charge is a cooldown AOE that deals mining and combat damage and cannot hurt the player.

Pickups within collection radius accelerate toward the ship and are credited exactly once. The utility module changes collection behavior (tractor pull versus scout drone). There is no cargo-capacity limit in the MVP.

## Station upgrades

Twelve run modifiers are purchased at Station with banked resources. Purchases are permanent on the profile (`PurchasedUpgradeIds`) and apply at the start of every subsequent run until the profile changes. They are not mid-run charge offers; mid-run upgrade pause/selection is not shipped.

Effects include weapon damage and fire rate, forked/pierce shots, shield and hull bonuses, speed and mobility cooldown, mining and tractor bonuses, and shock transit on mobility. Exact costs and modifiers live in [content-catalog.md](content-catalog.md) and `RunUpgradeCatalog`.

## Resources and run resolution

- `MAT_FERRITE`: common construction material.
- `MAT_LUMEN`: uncommon crystal from resource cells and salvage.
- `MAT_DATA_CORE`: elite research artifact.

Success banks all held resources. Failure retains 25% of held Ferrite, rounded down, and loses held Lumen/Data Cores. `RES_RECOVERY_PROTOCOLS` raises Ferrite retention to 50%. Banked resources, purchased research, and purchased station upgrades are never lost to failure.

The summary separates earned, banked, retained, and lost amounts and shows progress toward affordable research and upgrades.

## Station rules

Station exposes:

- Banked resource balances and previous-run result.
- Map: both environments and their access requirements; launch.
- Five-slot loadout editor with measured stat changes.
- Research graph, costs, prerequisites, and capability rewards.
- Upgrades catalog with banked costs and purchase state.
- Settings.

Only unlocked modules can be equipped. Unknown or incompatible saved module IDs fall back to the slot default with a visible diagnostic; the original save is preserved for recovery.

## Save contract

Persist at least:

- Save/content/generation/RNG schema versions.
- Profile seed and run index.
- Three banked resource balances.
- Purchased research IDs and unlocked environment IDs.
- Purchased station upgrade IDs.
- Equipped module ID by slot.
- Lifetime counters used by research gates.
- Settings and telemetry consent.

Purchased research and upgrades are never silently removed. Renamed IDs require explicit migrations.
