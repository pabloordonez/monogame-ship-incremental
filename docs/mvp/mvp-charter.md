# MVP Charter

## Purpose

Validate whether a low-attention combination of top-down space flight, combat, asteroid mining, station progression, extraction, and permanent research is fun across repeated 10–15 minute sessions.

The MVP is the first production slice. It must be maintainable and extensible, but it must not hide a weak loop behind content volume or speculative infrastructure.

## Player promise

Launch the Wayfarer Mk I, become noticeably stronger through loadout, station upgrades, and research, extract useful resources, and unlock permanent capabilities without losing purchased progress after failure.

## Core loop

1. Review resources, research, upgrades, and the previous result at Station.
2. Equip one module in each of five slots.
3. Optionally purchase station upgrades with banked resources.
4. Select an unlocked star environment on the Map.
5. Launch a deterministic seeded field.
6. Fight enemies and mine bounded destructible asteroid cells.
7. Complete the combined mining and combat objective.
8. Defeat the elite gunship and dwell in the extraction zone.
9. Bank resources or retain a smaller failure share.
10. Purchase research or upgrades, change the loadout, and repeat.

Target timing:

- Station and loadout: 1–3 minutes.
- Field: 8–12 minutes.
- Complete loop: 10–15 minutes.
- Field hard limit: 12 minutes.

## Fun hypotheses

### H1 — Controls and feedback feel immediately satisfying

Movement, aiming, firing, mining, impacts, destruction, and collection are responsive and readable without explanation.

### H2 — Station upgrades create meaningful power variation

Players can identify how at least one purchased station upgrade changes their tactics or power on a later run.

### H3 — Permanent growth motivates another run

A successful run usually advances a visible research or upgrade goal, and unlocked modules or capabilities create measurable differences.

### H4 — Randomness varies the experience without feeling unfair

Layouts, enemy compositions, and resource placement differ while every valid seed remains completable.

### H5 — Capability gates create curiosity

Players understand why Ion Veil is locked, can name the needed capability, and want to revisit it after research.

### H6 — Pressure remains compatible with low-attention play

Threats are telegraphed, pause is immediate, failure is understandable, and permanent purchases are never lost.

## Included content

- One ship hull: `SHIP_WAYFARER_MK1`.
- Keyboard/mouse and dual-stick gamepad input.
- Five module slots and meaningful alternatives.
- Three weapon behaviors: projectile, continuous beam, and homing missile.
- Shields, hull, recharge, dash/blink, damage, and collision.
- Bounded cell/chunk asteroid destruction, mining, and collection.
- Three normal enemy archetypes and one elite gunship modifier.
- Two star environments with distinct hazards and reward distributions.
- Twelve permanent station-purchased run upgrades.
- Twelve permanent research nodes.
- Three persistent resources.
- One combined objective and one extraction sequence.
- Title, Station, Map, Loadout, Research, Upgrades, Settings, Run, Pause, and Summary screens.
- Versioned local save/continue and lightweight local telemetry.
- First-pass pixel art, particles, UI, and essential sound placeholders.
- Player-facing title: Mine Your Own Business.

## Explicitly deferred

- Fuel, trading, shops, factions, dialogue, quests, and narrative campaign.
- Planets, orbit, probes, facilities, passive production, automation, and trade routes.
- Companions beyond the MVP scout module, fleets, and multiple player hulls.
- General-purpose destructible terrain or physics.
- Full procedural galaxy, black holes, galaxy-core simulation, and seamless travel.
- Procedural planets, moons, and rich nebula generation.
- Advanced shader stack, cinematics, full menu polish, cloud saves, and multiplayer.
- Music production; this MVP supplies a commissioning brief only.
- Mid-run pause-to-pick upgrade offers (superseded by station purchases).

Deferred features are future layers, not rejected ideas. Their expected extension seams are documented in [evolution-strategy.md](evolution-strategy.md).

## Product success criteria

- Median complete-loop time is 10–15 minutes.
- At least 70% of first-time players launch without help.
- First-five-run extraction rate is 50–75%.
- At least 60% voluntarily begin a fourth run.
- At least 70% use both combat and mining.
- At least 50% change loadout after unlocking an alternative.
- At least 60% report one station upgrade purchase as meaningful.
- Fewer than 10% of failures are blamed on unreadable damage or unclear rules.
- Players can explain the Ion Veil gate after inspecting it.

These are directional thresholds for small playtests, not universal benchmarks. Raw observations and sample size must accompany conclusions.

## Technical constraints

- Fixed-step simulation at 60 ticks per second.
- Gameplay/domain projects contain no MonoGame dependency.
- Stable string IDs are saved; enum ordinals, file paths, and ECS layouts are not.
- Run seed derives from profile seed, run index, environment ID, and version.
- Layout, encounter, drop, upgrade, and cosmetic randomness use independent streams.
- Launch increments and saves `runIndex`; restarting cannot reroll a run.
- Autosave occurs after launch, run resolution, research purchase, and upgrade purchase.
- Content validation rejects duplicate IDs, broken references, invalid ranges, and research cycles.
- Unknown saved IDs follow an explicit compatibility policy and produce diagnostics.

## Completion definition

The MVP is complete when:

- The full loop works without developer commands.
- Both environments are reached through normal progression.
- Every module, upgrade, and research node is exercisable.
- At least 10,000 generated seeds per environment for the current generation version pass headless reachability and reward validation (20,000 total for the MVP).
- Save/load preserves profile seed, run index, resources, research, upgrades, loadout, unlocks, and settings.
- Failure and extraction each return cleanly to Station.
- Automated tests, telemetry, observations, interviews, and surveys together can answer every hypothesis.
- A clean Windows machine can launch the packaged build.
