# P0_FOUNDATION Implementer Report

## Status

`IMPLEMENTATION_READY`

Candidate implementation commit: `38f2db9b429533653883a3d031299359b5d9fe7e`.

## Assumptions and verified platform facts

- Windows is the approved MVP platform; only the Windows Vulkan runtime is shipped.
- SDK `9.0.316` is pinned because the official MonoGame 3.8.5 templates target `net9.0`.
- NuGet flat-container metadata confirmed stable `3.8.5` packages for Framework Native, Windows Vulkan runtime, and Content Pipeline.
- The official `MonoGame.Templates.CSharp::3.8.5` `mgdesktopvk` template confirmed `MonoGamePlatform=DesktopVK`, Framework Native 3.8.5, and Windows Vulkan 3.8.5.
- The official 3.8.5 `mgcb` template confirmed the released console API: `ContentBuilder`, `ContentBuilderParams`, `ContentCollection`, `IncludeCopy<WildcardRule>`, Content Pipeline 3.8.5, and Framework Native 3.8.5 with `PrivateAssets=All`.
- The official game template still includes legacy Builder Task integration; it was deliberately not copied because the accepted package contract explicitly prohibits it.

## Requirement-to-change mapping

- Architecture: seven production/tool projects and seven focused test projects with a single composition root.
- ECS: generational reuse invalidates stale IDs; typed sparse-set stores use dense/sparse arrays and swap-back removal; queries sort IDs; structural commands apply at explicit synchronization; scheduler order is explicit and duplicate-safe.
- Simulation: neutral missing command frames, stale/future rejection, 60 Hz bounded catch-up, explicit dropped-time accounting, PCG32 and six streams, state/RNG hashing, no MonoGame or device queries.
- Content: one authored CC0 JSON placeholder is manifest-addressed, validated, copied by the released builder, loaded through `IAssetCatalog`, and intentionally remains P0-only data.
- Application/persistence: title, empty lobby, deterministic 180-tick run, summary and lobby return; run index saves at launch and profile saves at durable transitions; `C` continues a valid local profile.
- Save safety: versioned envelope, SHA-256 payload checksum, current-only registry, write-through temp, validation, atomic `File.Replace`, backup, backup recovery, and file-name/root confinement.
- Telemetry: disabled mode performs no writes; local JSONL includes schema/version fields; write failure is isolated.
- Operations: restore/content/build/test and launch scripts plus Windows CI.

## Gate evidence

1. Restore/content/build/test: `scripts/test.ps1` exited 0; build had 0 warnings/errors; 26 tests passed across all seven suites.
2. Content: clean and incremental builder executions exited 0 with 2 succeeded/0 failed. Generated output is ignored.
3. Bounded headless launch: `scripts/launch.ps1 -Smoke` exited 0 after content load, complete empty loop, save, and continue.
4. DesktopVK launch: bounded `--window-smoke` exited 0. Native output reported Vulkan initialization, SDL-required `VK_KHR_win32_surface`, `DESKTOPVK_CONTENT_READY`, selected NVIDIA GeForce RTX 3090, swapchain recreation, and `DESKTOPVK_WALKING_SKELETON_COMPLETE`.
5. Determinism: render-cadence comparison, PCG golden sequence, stream isolation, command validation, and schedule tests passed.
6. Architecture/ECS: MonoGame isolation, schedule, durable versions, component restrictions, stale IDs, sparse stores, queries, structural buffer, and scheduler tests passed.
7. Persistence/content/telemetry: all corresponding tests passed, including requested negative paths.
8. Legacy scan: repository source/project/workflow search returned no `.mgcb`, `MonoGame.Content.Builder.Task`, or `MonoGameContentReference`.

No gate is reported from an unexecuted test. Exact commands and results are in `commands-and-results.txt`.

## Changed contracts

This package establishes, rather than changes, version 1 contracts for save, content, generation, RNG, replay, and telemetry. It establishes the P0 scheduler subset, typed `ContentId`, `CommandFrame`, `ProfileSnapshot`, `IAssetCatalog`, `ITelemetrySink`, save compatibility result, ECS primitives, and the PCG32 sequence.

No contract-change proposal is required. Downstream changes to schedule, deterministic meaning, PRNG/seed derivation, persisted meaning, content schema, replay/hash semantics, or telemetry event meaning require affected-package review, version impact analysis, tests, and migrations where applicable.

## Risks and limitations

- No screenshot or human visual-quality review was captured by the noninteractive harness. The bounded graphical process did initialize the Windows Vulkan surface/swapchain and complete the state flow; manual evidence notes this limitation.
- NuGet vulnerability enumeration flags old transitive framework metadata pulled by the official MonoGame 3.8.5 Content Pipeline (high advisories on `Microsoft.NETCore.App 1.0.5`, JIT 1.0.7, `System.Net.Http 4.1.2`, and X509Certificates 4.1.0). The builder runs on .NET 9 and these legacy framework assemblies are not copied as application runtime files, but the official tool dependency graph should be rechecked when MonoGame publishes an updated package.
- P0 telemetry intentionally has no complete gameplay event catalog.
- P0 migration registry intentionally supports only schema 1 and explicitly rejects older/newer unsupported versions.
- The JSON data placeholder is the sole authored asset; Phase 1 does not produce P1 art/audio.

## Scope confirmation

No Phase 2 gameplay, balance content, final UI, enemies, mining, research, production assets, plugin system, generic engine, or deferred mechanic was implemented.
