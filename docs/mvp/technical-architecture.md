# Technical Architecture

## Goals

Support rapid mechanics experiments without coupling gameplay to MonoGame or building a general engine. The authoritative simulation is deterministic, headless-testable, and driven by validated versioned data. MonoGame 3.8.5 DesktopVK supplies the Windows host, rendering, audio, content loading, and device input.

## Proposed layout

```text
ShipGame.sln
Directory.Build.props
src/
  ShipGame.Domain/          # IDs, numerics, RNG, versions, contracts
  ShipGame.Ecs/             # entities, typed stores, queries, scheduler
  ShipGame.Gameplay/      # commands, components, systems, events, generation
  ShipGame.Content/         # definitions, runtime catalog, validation
  ShipGame.Persistence/     # DTOs, migrations, repositories
  ShipGame.Telemetry/       # versioned records and sinks
  ShipGame.Game/            # MonoGame composition, screens, input, render, audio
tools/
  ShipGame.ContentBuilder/  # MonoGame 3.8.5 C# Content Builder
content/
  source/
  definitions/
  generated/
tests/
  ShipGame.Ecs.Tests/
  ShipGame.Gameplay.Tests/
  ShipGame.Content.Tests/
  ShipGame.Persistence.Tests/
  ShipGame.Architecture.Tests/
  ShipGame.Game.SmokeTests/
```

Projects may be combined only if the dependency rules remain enforceable and the smaller layout improves delivery. Do not create an `Engine` project.

## Dependency direction

- `Domain` depends only on the .NET base class library.
- `Ecs` and `Content` depend on `Domain`.
- `Gameplay` depends on `Domain`, `Ecs`, and content contracts.
- `Persistence` depends on explicit profile/simulation snapshots and content contracts.
- `Telemetry` depends on versioned telemetry contracts.
- `Game` composes runtime projects and is the only project referencing MonoGame runtime APIs.
- `ContentBuilder` references content contracts and build tooling, never `Game`.
- Production dependency cycles are forbidden.

`ShipGame.Game` is the composition root. Prefer constructor composition and small factories; add a dependency-injection container only if composition becomes materially difficult.

## MonoGame baseline

- Pin MonoGame framework/runtime/content packages to exact 3.8.5 versions.
- Set `MonoGamePlatform` to `DesktopVK`.
- Reference `MonoGame.Framework.Native` and Windows Vulkan runtime.
- Use the released C# Content Builder console model, not legacy hand-maintained `.mgcb`.
- Target Windows first; retain case-correct asset IDs and platform-neutral domain code.
- Pin the .NET SDK in `global.json` after foundation verification against MonoGame templates.

## Lightweight ECS

### Entity identity

`EntityId` contains an index and generation. Reused indexes increment generation so stale references fail.

### Typed stores

`ComponentStore<T>` uses a sparse-set layout:

- Dense entity/component arrays for iteration.
- Sparse entity-index map for constant-time lookup.
- Swap-back removal.
- Queries iterate the smallest relevant store and probe other stores.
- No boxed component dictionary.

Order-sensitive systems sort entity IDs or generate explicitly ordered work. Dense insertion order is never an implicit rule.

### Components and owned simulation state

Components are data-only structs/records. They cannot load assets, invoke services, hold MonoGame objects, publish callbacks, or own RNG instances.

Tick counters, session state, named RNG streams, and immutable content catalogs live as fields on simulation facades (`FoundationSession`, `FlightCombatWorld`, orchestrators). There is no `WorldResource<T>` API on `World`.

### Structural changes and schedule

Entity create/destroy and component add/remove operations are buffered during iteration and applied only at synchronization points. The scheduler is an explicit ordered list; avoid reflection/attribute auto-ordering.

## Deterministic simulation

### Fixed timestep

Run at 60 simulation ticks/second. Accumulate clamped elapsed time, run a bounded number of catch-up ticks, and render an interpolated snapshot. Real elapsed time and interpolation never enter authoritative calculations. Report dropped simulation time instead of allowing an unbounded spiral.

### Numeric policy

For the Windows MVP, use constrained `float` values with quantized commands, stable iteration, deterministic tests, and state hashes. Do not claim cross-platform bitwise determinism. If networking or exact cross-platform replay becomes approved, record an ADR before moving authoritative movement to fixed point or quantized numerics.

### Commands and replay

Input adapters produce quantized command frames targeted at a tick. A diagnostic replay consists of:

- Initial snapshot or generation identity.
- Ordered command frames.
- Save/content/generation/RNG/replay versions.

### Randomness

Own a documented project PRNG; do not rely on unspecified `System.Random` behavior. Derive independent streams:

- `Layout`
- `Encounter`
- `Ai`
- `Loot`
- `Upgrade`
- `Cosmetic` (non-authoritative)

Seed derivation uses stable canonical IDs and versioned hashing. A new loot roll cannot perturb layout or upgrades.

Authoritative code cannot depend on wall-clock time, thread scheduling, unordered collection enumeration, locale parsing, random GUIDs, filesystem order, MonoGame state, or render frame count. The MVP simulation is single-threaded.

## Data-driven content

Typed IDs such as `WeaponId` and `ResearchId` wrap canonical strings. IDs remain stable across display-name and file changes.

Definitions hold values and relationships for modules, upgrades, resources, enemies, research, environments, encounters, sprites, animations, and cues. They select a small registry of built-in behavior keys; they do not name CLR types, load assemblies, or execute scripts.

The Content Builder:

1. Loads source definitions and the asset manifest.
2. Validates schemas, IDs, references, ranges, graphs, atlas regions, and licenses.
3. Emits a canonical runtime catalog.
4. Compiles textures, fonts, sounds, and effects.
5. Emits a manifest with schema/build versions and source/artifact hashes.
6. Fails on release-blocking warnings.

Runtime content is immutable. Development hot reload, if later added, rebuilds and validates a catalog rather than patching live state.

## Persistence and compatibility

Persist DTO snapshots, never ECS memory. Save headers contain:

- Save schema version.
- Content schema and catalog fingerprint.
- Generation and RNG algorithm versions.
- Replay and telemetry schema versions where applicable.
- Game build identifier and payload checksum.

Migrations are contiguous functions (`V1 -> V2`), tested against golden files. Existing profiles retain their generation version unless a deliberate conversion exists. Writes use temp file, flush, validation, atomic replacement, and one known-good backup.

Compatibility returns one explicit result: supported, migratable, incompatible-newer, corrupt, or missing-content. Never silently reset progress.

## Version policy

Version independently:

- Save schema: persisted shape/meaning.
- Content schema: definition shape/meaning.
- Content catalog/fingerprint: shipped values/assets.
- Generation: authoritative seeded output.
- RNG: sequence and seed derivation.
- Replay: commands/snapshots/hash semantics.
- Telemetry: event names/payload semantics.

A build number is diagnostic metadata, not a substitute.

## Performance budgets

- Use the reference machine, warm-up, capture method, and exact metrics defined in [validation-and-backlog.md](validation-and-backlog.md); every report records hardware, OS/driver, resolution, build/content versions, and capture tool.
- Gameplay sustains 60 fixed updates/second without accumulated tick debt.
- Rendering targets 60 frames/second at 1080p, with median frame time at most 16.7 ms, no more than 1% over 16.7 ms, and 99th percentile at most 33.3 ms over ten minutes.
- No sustained managed allocation in steady gameplay.
- No unbounded entity, event, particle, or audio growth over three runs.
- Spatial queries avoid all-pairs checks.
- Content lookups are typed/indexed after validation; canonical strings remain available for diagnostics.
- Profile save completes within 250 ms locally.

Optimize from traces. Do not introduce pooling, multithreading, or unsafe code without measured need and tests.

## Security and robustness

- Treat saves, definitions, manifests, and generated data as untrusted.
- Bound collection sizes, string lengths, numbers, decompressed content, and migration work.
- Resolve all content paths under known roots; reject traversal and arbitrary assembly/type loading.
- Write only inside the application save directory.
- Do not collect personal data or stable hardware fingerprints.
- Telemetry and content failures cannot corrupt authoritative state.
- No network services, executable mods, or script evaluation exist in the MVP.

## Architectural fitness tests

Automated checks enforce:

- Only `Game` references MonoGame runtime assemblies.
- `Domain`, `Ecs`, and `Gameplay` do not reference graphics, input, audio, filesystem, wall-clock, or JSON implementation namespaces.
- Project dependencies follow the allowed graph and contain no cycles.
- Components contain no services, assets, delegates, or framework objects.
- Systems register once in the approved order.
- Every persisted root declares required versions and has a contiguous migration path.
- Definitions use typed IDs and no CLR type names.
- Authoritative systems cannot instantiate unseeded RNGs.
- Headless tests run without graphics initialization.

## Decision records

Record an ADR when changing dependency direction, scheduler order, numeric policy, ECS storage, save support policy, generation compatibility, or platform target. Balance/content additions do not need ADRs unless they change a contract.

## Explicit non-goals

- Plugin/mod loader.
- Generic editor, scripting language, effect graph, physics engine, or scene graph.
- Networking, rollback, distributed simulation, or multithreaded ECS.
- Abstractions around MonoGame APIs with only one proven use.

Add shared abstractions only after at least two implemented mechanics demonstrate the same lifecycle and contract.
