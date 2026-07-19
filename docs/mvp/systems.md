# MVP System Contracts

## Shared rules

- Authoritative state lives in `ShipGame.Gameplay`, independent of MonoGame.
- Entities are opaque IDs; components are data-only; ordered systems own mutation.
- Entity/component structural changes are buffered to synchronization points.
- Immutable simulation events describe facts. Presentation and telemetry observe them but cannot change outcomes.
- Content is referenced by typed stable IDs, never display names, indexes, or paths.
- Player and AI use the same framework-neutral command model.
- Named seeded RNG streams own all authoritative randomness.

## Fixed tick order

1. Apply queued structural changes.
2. Consume validated commands.
3. Resolve session transitions and derived loadout/research statistics.
4. Advance the run clock, environment schedules/warnings, cooldowns, and shield timers; mark but do not yet finalize a reached deadline.
5. Update player/AI control intents and spawn decisions.
6. Integrate movement and rebuild the spatial index.
7. Detect collisions and weapon/mining contacts.
8. Resolve weapons, hazard activations, damage, mining, and hull death.
9. Resolve destruction, drops, and collection.
10. Resolve objective counters, elite-phase transitions, and extraction progress. Station upgrade modifiers are applied at run start, not via mid-run offers.
11. Resolve the terminal state in priority order: hull death, completed extraction, then reached deadline. Produce at most one exactly-once reward proposal.
12. Queue structural changes, publish the ordered event batch, and optionally calculate a deterministic state hash.

The composed host path (`ComposedRunOrchestrator`) co-steps flight combat, world-run, and mining/loot ECS each tick. Changing order requires an architecture decision and replay-test update.

## Application state

**Owns:** boot/title/Station/run/summary state, screen transitions, pause, run start/end orchestration.

**State/components:** `MetaSession`, `MetaScreen`, `EncounterIdentity`, run orchestrator handles, `PendingDespawn`.

**Commands/events:** `StartProfile`, `LaunchRun`, `Pause`, `EncounterStarted`, `RunResolved`, `ReturnToStation` (`EnterLobby` is a compatibility alias for Station entry).

**Invariants:** one active run; one player during a run; rewards commit once; Station mutation occurs only through profile transactions.

**Dependencies:** generation, progression, and persistence. Presentation observes application state/events but is not an inward dependency.

**Tests:** invalid transitions are atomic; duplicate completion cannot duplicate rewards; cleanup removes run entities only.

**Does not own:** rendering, save encoding, combat rules, or generation algorithms.

## Input and command system

**Owns:** conversion of device-neutral command frames into ship/UI intent.

**Components:** `ControlIntent`, `PlayerControlled`, `AiControlled`, `AimDirection`.

**Commands/events:** quantized `Move`, `Aim`, `Fire`, `Mine`, `Mobility`, `Interact`; `CommandRejected`.

**Invariants:** every command targets a tick; absent input is neutral; one source controls an entity; simulation never queries devices.

**Dependencies:** application, movement, combat, abilities.

**Tests:** keyboard/gamepad parity; stale/future command rejection; replayed frames produce identical hashes.

**Does not own:** bindings UI, device discovery, camera, or vibration.

## ECS world and lifecycle

**Owns:** entity generations, typed component stores, queries, buffered structural changes, ordered scheduling.

**Core types:** `EntityId`, `ComponentStore<T>`, `World`, `World.Query<TA,TB>()`, `CommandBuffer`, `SystemScheduler`, `ISystem`.

**Schedule names (contract):** Foundation runs `ConsumeCommands`, `SessionTransitions`, `RunClock`, `PublishAndHash`. Flight combat runs `ApplyFlightCombatStructuralChanges`, `ConsumeFlightCommands`, `AdvanceCombatTimers`, `ConsumeTemporaryModifiers`, `AiAndThreatDecisions`, `ResolveMobility`, `IntegrateFlightMovement`, `RebuildCombatSpatialIndex`, `DetectCombatCollisions`, `ResolveWeapons`, `ResolveMines`, `ResolveOrderedDamage`, `PublishCombatEventsAndHash`. Each phase is a named `ISystem` registered in that exact order.

**Structural sync:** Foundation has no ECS entities today. Combat marks destroys during a tick and applies them at the first phase next tick via `_pendingDestroy` in `ApplyFlightCombatStructuralChanges` (not `CommandBuffer`).

**Invariants:** stale IDs fail; component ownership is unique; query iteration cannot be invalidated; store insertion order cannot affect outcomes.

**Tests:** randomized add/remove/reuse; stale access; query correctness; deterministic results under different insertion orders.

**Does not own:** gameplay semantics, reflection-based auto-registration, serialization of raw stores, or rendering.

## Movement and spatial system

**Owns:** position, velocity, rotation, thrust, speed limits, colliders, bounds, broad-phase queries.

**Components:** `Transform2`, `Velocity2`, `Thruster`, `SpeedLimit`, `Collider`, `CollisionLayer`.

**Schedule phases (names, not separate types):** `IntegrateFlightMovement`, `RebuildCombatSpatialIndex`, `DetectCombatCollisions`; events include `CollisionDetected`.

**Invariants:** fixed tick duration; stable pair order; each pair resolves once; interpolation never writes simulation transforms.

**Dependencies:** control, resolved ship statistics, encounter bounds.

**Tests:** exact positions after known ticks; stable pairs; boundary and high-speed mobility cases.

**Does not own:** camera, particles, orbital physics, or arbitrary terrain.

## Combat and survivability

**Owns:** weapon state, projectiles/beams/missiles, target locks, cooldown/heat, shield, hull, damage, destruction.

**Components:** `Health`, `Shield`, `WeaponMount`, `WeaponState`, `Projectile`, `Homing`, `DamageSource`, `Faction`, `Destroyed`.

**Schedule phases (names):** `AdvanceCombatTimers`, `ResolveWeapons`, `ResolveOrderedDamage`; events include `WeaponFired`, `ShieldDepleted`, `HullDamaged`, `EntityDestroyed`.

**Invariants:** a hit applies once; shield precedes hull; values stay in bounds; destroyed entities cannot act or reward twice; behavior keys select built-in composition.

**Dependencies:** movement, commands, content, progression, drops.

**Tests:** exact fire cadence/heat; stable simultaneous hits; seeker target loss; shield boundary ticks; friendly-fire policy.

**Does not own:** effects, sound, damage text, or future weapon scripting.

## Abilities

**Owns:** dash/blink availability, cooldown, invulnerability window, valid destination, and endpoint effects.

**Components:** `MobilityAbility`, `AbilityCooldown`, `TemporaryInvulnerability`.

**Schedule phases (names):** `ResolveMobility`; events include `AbilityActivated`, `AbilityRejected`.

**Invariants:** one activation per accepted command; invalid blink shortens safely; cooldown cannot become negative; endpoint shock occurs once.

**Dependencies:** commands, movement, collision, upgrades.

**Tests:** obstruction, cooldown boundary, invulnerability, shock transit single activation.

**Does not own:** general skill trees or arbitrary ability scripting.

## AI and spawning

**Owns:** normal enemy decisions, threat budget, spawn timing/anchors, elite modifier application.

**Components:** `AiBrain`, `Target`, `SpawnAnchor`, `ThreatValue`, `Elite`.

**Schedule phases (names):** `AiAndThreatDecisions`; events include `EnemySpawned`, `EliteActivated`.

**Invariants:** spawn is off-camera and at least 450 units away; active caps hold; AI uses encounter RNG; elite spawn cap is environment-aware (1 on Cinder Belt, 2 simultaneous on Ion Veil); Ion Veil may assign rare beam/seeker mounts to non-elite interceptor/gunship threat spawns.

## Asteroid field (composed run)

**Owns:** sized asteroid descriptors (S/M/L), drifting combat obstacles mirrored to mineable cells, loot burst on break, ore-readable presentation tiers.

**Invariants:** each generated field includes all three sizes; obstacle radii match size; combat positions sync to mining `WorldPosition` each tick; projectile-obstacle contact applies knockback then destroys the projectile; loot pickups spawn at the break center with outward `PickupBurst` before tractor pull; presentation draws size×kind×health-tier rock bitmaps and sprays `asteroids/debris/*` chunks on break.

**Dependencies:** session timer, spatial queries, content, objective.

**Tests:** archetype decisions, caps, anchor validation, same-seed composition.

**Does not own:** faction diplomacy, adaptive difficulty, or presentation telegraphs.

## Procedural encounter generation

**Owns:** immutable run descriptor, sectors/corridors, asteroid clusters, anchors, objectives, hazards, reachability validation.

**Records:** `GenerationIdentity`, `EnvironmentDescriptor`, `SectorDescriptor`, `HazardSchedule`, `ObjectiveDescriptor`.

**Systems/events:** staged `EncounterGenerator`, `EncounterValidator`, `EncounterSpawner`; `EncounterGenerated`, `GenerationFallbackUsed`.

**Invariants:** identity includes environment, seed, content, generation, and RNG versions; no wall clock/enumeration-order input; all required locations reachable; objective resources guaranteed.

**Dependencies:** content catalog, named RNG streams, access gate.

**Tests:** golden seeds; 10,000-seed invariant sweep per environment for the current generation version (20,000 total for the MVP); stream isolation; deterministic fallback.

**Does not own:** full galaxy topology, visual nebulae, or arbitrary world simulation.

## Mining, loot, and collection

**Owns:** asteroid-cell mining health, mining contacts, deterministic drops, collectibles, pickup attraction and credit.

**Components / orchestrator state:** `MineableCell` and related mining/loot ECS components; mining contacts and tool range live on `ComposedRunOrchestrator` (laser range 130 wu); loot tables resolve in `LootGenerationSystem`.

**Systems/events:** `MiningSystem`, `LootGenerationSystem`, `CollectionSystem`; cell-break, loot-spawn, and resource-collected facts/events as published by the composed path.

**Invariants:** no over-extraction; positive quantities; one credit/despawn; tables resolve valid IDs; drops use the loot stream.

**Dependencies:** collision, content, objective, profile transaction.

**Tests:** break tick/yield; simultaneous collection; same-seed drops; resource conservation.

**Does not own:** free-form terrain, cargo logistics, planet extraction, or trade.

## Station upgrades (run modifiers)

**Owns:** station purchase of the twelve upgrade IDs, profile `PurchasedUpgradeIds`, folding into `TemporaryModifiers` / combat modifiers at run start, and clearing in-run modifier state when a run ends (profile purchases persist).

**Components/resources:** `TemporaryModifiers`, `TemporaryCombatModifiers`; catalog `RunUpgradeCatalog`.

**Commands/events:** `PurchaseUpgrade` (Station/Upgrades screen). Legacy mid-run kinds `UpgradeThresholdReached`, `UpgradeOffered`, and `UpgradeSelected` exist as event enum values but are handled as NoOp in the composed path; there is no mid-run offer UI.

**Invariants:** catalog has twelve unique IDs; each ID may be purchased once per profile; costs debit banked resources atomically; folded modifiers apply for subsequent runs; purchases survive failure.

**Dependencies:** profile transactions, persistence, combat/mining derived stats.

**Tests:** purchase cost/duplicate rejection; fold order determinism; modifiers granted at run start; mid-run offer path remains disabled.

**Does not own:** research purchases or screen layout pixels.

## Environment, objective, and run resolution

**Owns:** environment hazard schedules/effects, run clock, objective counters, elite-phase transition, extraction dwell progress, success/failure decision, and exactly-once reward proposal.

**Facade/state (not all ECS components):** `WorldRun` owns `RunTick`, `ObjectiveProgress`, `RunPhase`, `ExtractionProgressTicks`, and reward proposal; hazards resolve through `EnvironmentHazardSystem` / schedule descriptors on the run.

**Commands/events (`WorldRunEventKind`):** `HazardWarned`, `HazardDamageRequested`, `ObjectiveCompleted`, `EliteActivationRequested`, `EliteDefeated`, `DataCoreDropped`, `ExtractionActivated`, `ExtractionProgressed`, `ExtractionReset`, `CollapseWarning`, `RunSucceeded`, `RunFailed`, `RewardProposed` (plus legacy upgrade kinds handled as NoOp). Flight `Interact` is reserved and does not drive extraction; progress is continuous in-zone dwell.

**Invariants:** pause and meta screens do not advance the run clock; environment schedules derive only from the run descriptor; objective counters consume ordered facts once; extraction cannot activate before elite defeat; extraction progress advances while the player remains in the zone and resets on leave; at most one terminal result/reward proposal exists during a run and exactly one exists after terminal resolution; hull death takes precedence over same-tick extraction, while completed extraction takes precedence over the same-tick deadline.

**Dependencies:** session, generation descriptor, combat/mining events, spatial queries, application pause, profile transaction boundary.

**Tests:** early/late objective completion; each hazard schedule/cover rule; elite transition; extraction enter/leave; 10:00 warning and 12:00 boundary; simultaneous death/extraction ordering; duplicate-event/reward rejection.

**Does not own:** environment visuals/audio, profile banking, research, or arbitrary quest scripting.

## Profile, research, loadout, upgrades, and access

**Owns:** banked resources, lifetime counters, research graph/purchase, station upgrade purchases, capabilities, unlocked destinations, equipped modules, derived statistics.

**Profile records:** `ResourceBalances`, `ResearchState`, `PurchasedUpgradeIds`, `CapabilitySet`, `ShipLoadout`, `LifetimeCounters`.

**Commands/events:** `PurchaseResearch`, `PurchaseUpgrade`, `EquipModule`, `SelectDestination`; `ResearchPurchased`, `LoadoutChanged`, `TravelRejected`.

**Invariants:** balances nonnegative; transactions atomic/idempotent; prerequisites and gates pass; slots are compatible; gates query capabilities; derived stats rebuild deterministically.

**Dependencies:** content, run resolution, persistence, application.

**Tests:** cost/prerequisite rejection; graph cycles; duplicate transaction; default fallback; Ion Veil gate; upgrade purchase persistence.

**Does not own:** screen layout, shops, crafting, factions, or economy.

## Persistence

**Owns:** explicit save DTOs, compatibility check, migrations, atomic replacement, backup, recovery diagnostics.

**Services/events:** `SaveSnapshotBuilder`, `SaveRepository`, `SaveMigrationPipeline`; `SaveCompleted`, `SaveFailed`, `SaveMigrated`.

**Invariants:** all durable versions declared; snapshots only at safe boundaries; temp-write/flush/validate/replace; no silent reset; ECS internals never serialized.

**Dependencies:** profile/session snapshots, content compatibility, filesystem adapter.

**Tests:** current round trip; golden old saves; sequential migrations; interrupted write; corruption/newer version; unknown ID.

**Does not own:** cloud sync, accounts, or gameplay mutation.

## Presentation, UI, content, and audio

**Owns:** MonoGame adapters, render snapshots/interpolation, cameras, screens, sprites, particles, UI, audio cues, runtime asset resolution.

**Presentation state:** `SpriteBinding`, `AnimationState`, `CameraTarget`, `AudioCueState`; kept outside the authoritative world where possible.

**Invariants:** rendering never mutates simulation; dropped effects do not change gameplay; UI emits validated commands; runtime uses asset IDs.

**Dependencies:** MonoGame DesktopVK, compiled content, simulation snapshots/events.

**Tests:** all IDs load; event mappings; render extraction is read-only; simulation assemblies have no MonoGame references.

**Does not own:** gameplay timers, damage, randomness, or save migration.

## Telemetry

**Owns:** conversion of post-tick facts to versioned local records and sink failure isolation.

**Services:** `TelemetryTranslator`, `ITelemetrySink`, local JSONL sink.

**Invariants:** optional; no PII; failure never blocks gameplay; no per-frame/raw-input logging; schema version included.

**Dependencies:** simulation/application events, consent/settings.

**Tests:** schema validation, disabled mode, sink failure, aggregation.

**Does not own:** balance decisions, network analytics, or authoritative state.
