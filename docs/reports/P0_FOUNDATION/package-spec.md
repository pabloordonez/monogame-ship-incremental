# P0_FOUNDATION Package Specification

## Identity

- Package: `P0_FOUNDATION`
- Branch: `p0-foundation`
- Pinned base: `a443f89c920e37b2d46385a1d18b2e34efe4892f`
- Source: `docs/mvp/agent-workflow.md`, Phase 1 lines 41–85.
- Scope: the smallest production walking skeleton through each durable boundary.

## Owned deliverables and implementation map

| Requirement | Implementation |
|---|---|
| Solution/dependency layout | `ShipGame.sln`, `src/`, `tools/`, `tests/`, `Directory.Build.props`, `global.json` |
| MonoGame 3.8.5 DesktopVK | Exact `MonoGame.Framework.Native` and `MonoGame.Runtime.Windows.Vulkan` 3.8.5 references in `ShipGame.Game`; `MonoGamePlatform=DesktopVK` |
| Released C# Content Builder | `tools/ShipGame.ContentBuilder`, exact 3.8.5 Pipeline/Native references, `ContentBuilder` subclass and `ContentCollection`; no `.mgcb`, Builder Task, or `MonoGameContentReference` |
| Minimal loadable content | One CC0 authored JSON placeholder plus its versioned manifest under `content/source/data` |
| MonoGame-free authority | `Domain`, `Ecs`, and `Simulation` have no MonoGame references; architecture test enforces it |
| ECS | Generational `EntityId`, typed sparse-set `ComponentStore<T>`, `World`, buffered structural commands, explicit scheduler |
| Determinism | 60 Hz fixed step, tick-targeted neutral commands, owned PCG32, named isolated streams, deterministic hash including RNG state |
| Application shell | `Title -> Lobby -> Run -> Summary -> Lobby`, deterministic empty run, save boundaries, continue path |
| Content contract | Typed IDs, manifest root/path/provenance validation, duplicate ID/source and missing source/reference failures |
| Persistence | Independent version header, checksum, current-only migration classification, flushed temp, atomic replace, known-good backup, corruption recovery, path safety |
| Telemetry | Versioned records, disabled sink, local JSONL sink, failure isolation |
| Quality/operations | Windows PowerShell scripts, Windows CI, seven test projects, headless and bounded graphical smoke paths |

## Published package map

- `ShipGame.Domain`: stable IDs, durable versions, PRNG, stream derivation, stable hashing, profile snapshot.
- `ShipGame.Ecs`: entity/component lifetime, structural synchronization, ordered scheduler.
- `ShipGame.Content`: manifest/definition validation and minimal runtime asset catalog.
- `ShipGame.Simulation`: authoritative foundation state machine and fixed-step driver.
- `ShipGame.Persistence`: save compatibility and atomic local repository.
- `ShipGame.Telemetry`: optional telemetry records and sinks.
- `ShipGame.Game`: only runtime presentation/composition host.
- `ShipGame.ContentBuilder`: separate released code-centric build tool.

Dependency direction is `Domain <- Ecs/Content <- Simulation`; persistence consumes Domain/Content; telemetry consumes Domain; Game composes runtime packages. ContentBuilder consumes Content contracts and official build tooling.

## Foundation scheduler

1. `ApplyStructuralChanges`
2. `ConsumeCommands`
3. `SessionTransitions`
4. `RunClock`
5. `PublishAndHash`

This is the implemented P0 subset of the full fixed order. Phase 2 inserts owned systems only at documented positions and updates deterministic schedule tests. Hidden/reflection registration is prohibited.

## Gate definition

1. Clean restore/build/test/content-build/launch succeeds.
2. DesktopVK initializes a Vulkan surface/swapchain and the walking skeleton completes.
3. Same seed/commands hash identically across render cadences.
4. Authoritative assemblies have no MonoGame dependency.
5. Save round trip, corruption recovery, atomic replace, interrupted-temp behavior, newer-version result, and path safety pass.
6. Duplicate/missing content failures pass.
7. No legacy MGCB source of truth exists.

## Explicit exclusions

No gameplay, balance catalog, final UI, enemies, mining, research, production assets/audio, procedural generation, plugins, generic engine, or deferred mechanics are included.
