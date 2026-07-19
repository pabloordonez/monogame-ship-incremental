# MVP Content Catalog

Stable IDs are serialization keys. Display names may change; IDs may not change without migration. Distances are world units and times are seconds.

## Resources

| ID | Name | Use | Expected successful-run yield |
|---|---|---|---|
| `MAT_FERRITE` | Ferrite | Common research and fabrication material | 35–65 |
| `MAT_LUMEN` | Lumen Crystal | Advanced technology and access research | 1–4 |
| `MAT_DATA_CORE` | Data Core | Elite research artifact | Exactly 1 |

Standard Ferrite cells yield 2–4. Lumen cells yield 1.

## Environments

### `ENV_CINDER_BELT` — Kestrel-442

- Available on profile creation.
- Warm K-type star; dense asteroid cover.
- Resource-cell weights: 85% Ferrite, 15% Lumen.
- Solar flare starts near 1:00, then repeats every seeded `75 ± 10` seconds.
- Four-second directional warning; 25 shield-first damage.
- Large asteroids provide complete cover.
- Enemy health/damage multipliers: `1.00/1.00`.

### `ENV_ION_VEIL` — Vela-91

- Requires capability `CAP_TRAVEL_ION_VEIL`, granted by `RES_NAV_ION_VEIL`.
- Cool A-type star; sparse asteroids and ion clouds.
- Resource-cell weights: 70% Ferrite, 30% Lumen.
- Shield recharge delay increases by 1.5 seconds.
- Every 45 seconds, three circles warn for 2.5 seconds then deal 30 shield-first damage.
- Enemy health/damage multipliers: `1.20/1.15`.

## Enemies

### `ENM_INTERCEPTOR`

- Fast flanker: hull 28, speed 190, preferred range 120.
- Three-shot burst, 6 damage each, 0.15-second spacing, 2.2-second cooldown.
- Telegraph: 0.45-second muzzle flash.
- Retreats for one second after firing.

### `ENM_GUNSHIP`

- Ranged pressure: hull 55, speed 105, preferred range 380.
- Plasma bolt: 18 damage, 2.8-second cooldown.
- Telegraph: 0.8-second aim line.
- Maintains range and avoids asteroid cells.

### `ENM_SAPPER`

- Area denial: hull 42, speed 130, preferred range 260.
- Deploys a mine every 3.5 seconds; maximum two active.
- Mine arms in one second, lasts eight, and deals 24 damage in radius 75.
- Telegraph: flashing mine and radius ring.

### `MOD_ELITE_PROTOCOL`

Applied to one seeded archetype:

- Scale `1.35`; hull `2.75x`; damage `1.35x`; speed `1.10x`; cooldown `0.80x`.
- Adds elite outline, arena marker, and exactly one Data Core drop.
- Only one elite may exist per run.

## Ship modules

### Weapon — `SLOT_WEAPON`

**`MOD_WEAPON_PULSE` — Kestrel Pulse Cannon** (default)

- 10 damage; 5 shots/second; projectile speed 700; range 650.
- Reliable discrete projectile behavior with no ammunition.

**`MOD_WEAPON_BEAM` — Helios Beam Emitter**

- Requires `RES_WEAPON_BEAM`.
- Continuous 30 damage/second; range 520.
- Overheats after three firing seconds; cools after two idle seconds.
- Applies 20% combat damage as mining damage.

**`MOD_WEAPON_SEEKER` — Warden Seeker Rack**

- Requires `RES_WEAPON_SEEKER`.
- Launches two missiles, 16 damage each, every 0.6 seconds.
- Projectile speed 480; lock range 600; turn rate 150 degrees/second.
- Homes when a target is inside a 35-degree aim cone; otherwise flies straight along aim.

### Mining — `SLOT_MINING`

**`MOD_MINING_LASER` — Mole Mining Laser** (default)

- Continuous 25 mining damage/second; range 260.

**`MOD_MINING_CHARGE` — Seismic Charge**

- Requires `RES_MINING_SEISMIC`.
- Three-second cooldown; aimed range 300; radius 110.
- 65 mining damage and 12 combat damage; cannot hurt the player.

### Shield — `SLOT_SHIELD`

**`MOD_SHIELD_CAPACITOR` — Aegis Capacitor** (default)

- Capacity 60; recharge 12/second; delay 3.

**`MOD_SHIELD_REFLECTIVE` — Bastion Reflective Screen**

- Requires `RES_SHIELD_REFLECTIVE`.
- Capacity 45; recharge 10/second; delay 2.5.
- Returns 20% of absorbed projectile damage; hazards are not reflected.

### Engine — `SLOT_ENGINE`

**`MOD_ENGINE_VECTOR` — Vector Thrusters** (default)

- Maximum speed 220.
- Dash: distance 180, duration 0.18, cooldown 4; invulnerable during dash.

**`MOD_ENGINE_BLINK` — Comet Blink Drive**

- Requires `RES_ENGINE_BLINK`.
- Maximum speed 200.
- Blink: distance 260, cooldown 6; passes through entities.
- Invalid destinations shorten to the nearest valid position.

### Utility — `SLOT_UTILITY`

**`MOD_UTILITY_TRACTOR` — Magpie Tractor Array** (default)

- Adds 140 pickup-radius units; pull speed 260.
- Reveals nearby pickups through asteroids.

**`MOD_UTILITY_DRONE` — Firefly Scout Drone**

- Requires `RES_UTILITY_DRONE`.
- One invulnerable orbiting drone; attacks nearest enemy within 450.
- Deals 8 damage every 0.8 seconds; does not mine or collect.

## In-run upgrades

All twelve are unique per run. Percentage modifiers stack multiplicatively; flat additions apply first.

| ID | Name | Effect |
|---|---|---|
| `UPG_OVERCHARGED_MUNITIONS` | Overcharged Munitions | Weapon damage `+20%`. |
| `UPG_RAPID_CYCLING` | Rapid Cycling | Fire rate or beam tick output `+18%`. |
| `UPG_FORKED_OUTPUT` | Forked Output | Pulse adds an 85%-damage angled projectile; beam forks for 45%; seeker adds one 60%-damage missile. |
| `UPG_PENETRATING_FIELD` | Penetrating Field | Pulse pierces one enemy; beam passes through one target; seeker explosion radius 55. |
| `UPG_SHIELD_RESERVOIR` | Shield Reservoir | Maximum/current shield `+30`. |
| `UPG_FAST_REBOOT` | Fast Reboot | Recharge delay `-1.0`; recharge rate `+20%`. |
| `UPG_REINFORCED_FRAME` | Reinforced Frame | Maximum/current hull `+25`. |
| `UPG_THRUSTER_OVERCLOCK` | Thruster Overclock | Maximum speed `+15%`. |
| `UPG_MOBILITY_LOOP` | Mobility Loop | Dash/blink cooldown `-30%`. |
| `UPG_FRACTURE_LENS` | Fracture Lens | Mining damage `+30%`; Ferrite cell yield `+20%`, rounded up. |
| `UPG_MAGNETIC_SWEEP` | Magnetic Sweep | Pickup radius `+90`; pull speed `+50%`. |
| `UPG_SHOCK_TRANSIT` | Shock Transit | Mobility endpoint emits a radius-90, 20-damage shockwave. |

## Research tree

Costs are Ferrite/Lumen/Data Core. Gates use profile counters and are checked in addition to dependencies.

| ID | Name | Cost F/L/D | Dependencies | Gate | Reward |
|---|---|---:|---|---|---|
| `RES_HULL_REINFORCEMENT` | Layered Bulkheads | 25/0/0 | — | — | Base hull `+15`. |
| `RES_SHIELD_REFLECTIVE` | Reflective Harmonics | 45/1/1 | `RES_HULL_REINFORCEMENT` | 1 extraction | Unlock Reflective Screen. |
| `RES_WEAPON_BEAM` | Coherent Emitters | 35/0/1 | — | 1 extraction | Unlock Beam Emitter. |
| `RES_WEAPON_SEEKER` | Seeker Telemetry | 60/2/2 | `RES_WEAPON_BEAM` | 20 lifetime kills | Unlock Seeker Rack. |
| `RES_MINING_SEISMIC` | Resonance Charges | 30/0/1 | — | 60 lifetime Ferrite | Unlock Seismic Charge. |
| `RES_MINING_ASSAY` | Spectral Assay | 50/2/1 | `RES_MINING_SEISMIC` | 40 resource cells | Ferrite yield `+15%`, rounded up. |
| `RES_ENGINE_TUNING` | Vector Calibration | 25/0/0 | — | — | Maximum speed `+8%`. |
| `RES_ENGINE_BLINK` | Folded Transit | 60/2/2 | `RES_ENGINE_TUNING`, `RES_SHIELD_REFLECTIVE` | 3 extractions | Unlock Blink Drive. |
| `RES_UTILITY_DRONE` | Autonomous Firefly | 40/0/1 | `RES_HULL_REINFORCEMENT` | 1 elite kill | Unlock Scout Drone. |
| `RES_TRACTOR_CALIBRATION` | Wideband Recovery | 45/1/0 | `RES_UTILITY_DRONE` | 150 lifetime Ferrite | Base pickup radius `+35`. |
| `RES_NAV_ION_VEIL` | Ion Sheathing | 80/4/3 | `RES_SHIELD_REFLECTIVE`, `RES_ENGINE_BLINK` | 5 extractions, 5 elites | Grant `CAP_TRAVEL_ION_VEIL`. |
| `RES_RECOVERY_PROTOCOLS` | Recovery Protocols | 70/2/3 | `RES_WEAPON_SEEKER`, `RES_MINING_ASSAY` | 1 Ion Veil extraction | Failed-run Ferrite retention becomes 50%. |

Purchasing all nodes costs 565 Ferrite, 14 Lumen, and 15 Data Cores. The graph is acyclic. Research rewards apply only to subsequent runs and cannot be lost.

## Objective and extraction IDs

- `OBJ_FIELD_PROOF`: collect 30 run Ferrite and destroy eight normal enemies.
- `EXT_STANDARD_GATE`: hold interact for six seconds after elite defeat.

## Validation rules

Build fails on duplicate IDs, missing references, invalid slot compatibility, negative costs, research cycles, unreachable capability gates, impossible upgrade pools, invalid ranges, or a catalog where an MVP content item cannot be exercised.
