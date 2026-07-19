# MVP Content Catalog

Stable IDs are serialization keys. Display names may change; IDs may not change without migration. Distances are world units and times are seconds.

**Authority note:** Combat weapon and enemy combat stats are owned by `FlightCombatBehaviorRegistry` and related Gameplay code. Module presentation values in `mvp-catalog.json` may lag runtime where noted. Station upgrade costs and apply rules are owned by `RunUpgradeCatalog`. Research graph is owned by `ResearchCatalog`.

## Resources

| ID | Name | Use | Expected successful-run yield |
|---|---|---|---|
| `MAT_FERRITE` | Ferrite | Common research, upgrades, and fabrication material | 35–65 |
| `MAT_LUMEN` | Lumen Crystal | Advanced technology and access research | 1–4 |
| `MAT_DATA_CORE` | Data Core | Elite research artifact | Exactly 1 |

Content definition yields for standard Ferrite cells are 2–4 and Lumen cells 1; composed-run loot rolls may differ (ordinary scrap, kill salvage, Fracture Lens, assay). Prefer Gameplay loot rules when documenting runtime economy.

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
- Shield recharge delay increases by 1.5 seconds (90 ticks).
- Every 45 seconds, three circles warn for 2.5 seconds then deal 30 shield-first damage.
- Enemy health/damage multipliers: `1.20/1.15`.

## Enemies

### `ENM_INTERCEPTOR`

- Fast flanker: hull 28, speed 190, preferred range 120.
- Burst fire, 6 damage, cadence 132 ticks.
- Telegraph: muzzle flash.
- Strafes and retreats after firing.

### `ENM_GUNSHIP`

- Ranged pressure: hull 55, speed 105, preferred range 380.
- Plasma bolt: 18 damage, cadence 168 ticks.
- Telegraph: aim line.
- Maintains range; also the elite base archetype.

### `ENM_SAPPER`

- Area denial: hull 42, speed 130, preferred range 260.
- Deploys mines; mine damage 24, cadence 210 ticks.
- Telegraph: flashing mine and radius ring.

### `MOD_ELITE_PROTOCOL`

Applied to the elite spawn (always `ENM_GUNSHIP` in the composed run):

- Scale `1.35`; hull `2.75x`; damage `1.35x`; speed `1.10x`; cooldown `0.80x`.
- Adds elite outline, arena marker, and exactly one Data Core drop.
- Only one elite may exist per run.

## Ship modules

### Weapon — `SLOT_WEAPON`

**`MOD_WEAPON_PULSE` — Kestrel Pulse Cannon** (default)

- 10 damage; 5 shots/second (cadence 12 ticks); projectile speed 700; range 650.
- Reliable discrete projectile behavior with no ammunition.

**`MOD_WEAPON_BEAM` — Helios Beam Emitter**

- Requires `RES_WEAPON_BEAM`.
- Continuous hitscan **100 DPS**; range **600**; lock cone 24°.
- Builds heat while firing (0.5/tick); locks at heat 180 and vents while overheated; cools at 3/tick when idle or locked.
- `mvp-catalog.json` still lists older 30 DPS / 520 range presentation values; runtime combat uses the registry above.

**`MOD_WEAPON_SEEKER` — Warden Seeker Rack**

- Requires `RES_WEAPON_SEEKER`.
- Launches two missiles, 16 damage each, every 0.6 seconds (cadence 36 ticks).
- Projectile speed 480; lock range 600; turn rate 150 degrees/second.
- Homes when a target is inside a 35-degree aim cone; otherwise flies straight along aim.

### Mining — `SLOT_MINING`

**`MOD_MINING_LASER` — Mole Mining Laser** (default)

- Continuous 25 mining damage/second.
- Composed-run mining range: **130** world units (`ComposedRunOrchestrator.MiningRangeWorldUnits`).
- `mvp-catalog.json` may still list presentation range 260; runtime uses 130.

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
- Replaces tractor behavior when equipped.

## Station upgrades

Twelve unique upgrades purchased at Station with banked resources. Purchases persist on the profile and apply at run start via folded modifiers. Mid-run charge/offer selection is not shipped.

Percentage modifiers stack multiplicatively through basis-point folding; flat additions apply as coded. Forked Output secondary damage is per weapon: pulse ×0.85, seeker ×0.6, beam fork tick damage ×0.45.

| ID | Name | Cost F/L/D | Effect |
|---|---|---|---|
| `UPG_OVERCHARGED_MUNITIONS` | Overcharged Munitions | 30/0/0 | Weapon damage `+20%`. |
| `UPG_RAPID_CYCLING` | Rapid Cycling | 30/0/0 | Fire rate `+18%`. |
| `UPG_FORKED_OUTPUT` | Forked Output | 40/1/0 | Adds one forked shot or beam fork; secondary damage pulse ×0.85 / seeker ×0.6 / beam ×0.45. |
| `UPG_PENETRATING_FIELD` | Penetrating Field | 40/1/0 | Pierce one additional target. |
| `UPG_SHIELD_RESERVOIR` | Shield Reservoir | 35/0/1 | Maximum/current shield `+30`. |
| `UPG_FAST_REBOOT` | Fast Reboot | 45/1/1 | Recharge delay `-1.0s`; recharge rate `+20%`. |
| `UPG_REINFORCED_FRAME` | Reinforced Frame | 35/0/0 | Maximum/current hull `+25`. |
| `UPG_THRUSTER_OVERCLOCK` | Thruster Overclock | 30/0/0 | Maximum speed `+15%`. |
| `UPG_MOBILITY_LOOP` | Mobility Loop | 40/1/0 | Dash/blink cooldown `-30%`. |
| `UPG_FRACTURE_LENS` | Fracture Lens | 45/1/1 | Mining damage `+30%`; Ferrite yield bump when loot applies Fracture Lens. |
| `UPG_MAGNETIC_SWEEP` | Magnetic Sweep | 40/0/1 | Pickup radius `+90`; pull speed `+50%`. |
| `UPG_SHOCK_TRANSIT` | Shock Transit | 50/2/1 | Mobility endpoint emits a radius-90, 20-damage shockwave. |

## Research tree

Costs are Ferrite/Lumen/Data Core. Gates use profile counters and are checked in addition to dependencies.

| ID | Name | Cost F/L/D | Dependencies | Gate | Reward |
|---|---|---:|---|---|---|
| `RES_HULL_REINFORCEMENT` | Layered Bulkheads | 25/0/0 | — | — | Base hull `+15`. |
| `RES_SHIELD_REFLECTIVE` | Reflective Harmonics | 45/1/1 | `RES_HULL_REINFORCEMENT` | 1 extraction | Unlock Reflective Screen. |
| `RES_WEAPON_BEAM` | Coherent Emitters | 35/0/1 | — | 1 extraction | Unlock Beam Emitter. |
| `RES_WEAPON_SEEKER` | Seeker Telemetry | 60/2/2 | `RES_WEAPON_BEAM` | 20 lifetime kills (normal + elite) | Unlock Seeker Rack. |
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
- `EXT_STANDARD_GATE`: remain in the extraction zone for six seconds after elite defeat.

## Validation rules

Build fails on duplicate IDs, missing references, invalid slot compatibility, negative costs, research cycles, unreachable capability gates, invalid ranges, or a catalog where an MVP content item cannot be exercised.
