# P0 Foundation Extension Note

## Data-only additions

Using existing behavior contracts, later packages may add:

- manifest entries and validated source files;
- stable content definitions and references;
- telemetry event instances using the version 1 record shape;
- additional save payload values only after assigning ownership and assessing save-version impact.

Content additions remain data-only only when they do not introduce a new behavior key, authoritative meaning, schedule position, or migration.

## Additions requiring focused code

- A gameplay behavior requires data-only components and one or more focused systems at documented scheduler positions.
- A new command requires a framework-neutral, quantized, tick-targeted field and replay/hash compatibility review.
- New authoritative randomness must use an owned named stream. Adding or changing streams or seed derivation requires RNG/generation/replay impact review and golden tests.
- A new persisted subsystem contributes explicit DTO state at safe boundaries; raw ECS stores remain forbidden.
- New presentation assets bind through stable IDs and `IAssetCatalog`; presentation never mutates simulation.
- New telemetry translations observe post-tick facts and cannot block simulation.

## Stable contracts established by P0

- Generational `EntityId`, typed sparse stores, buffered structural synchronization, sorted order-sensitive work.
- Explicit scheduler; no reflection/attribute registration.
- 60 Hz authority, neutral absent input, bounded catch-up, and dropped-time reporting.
- PCG32 sequence, canonical stream names, versioned seed/hash meaning.
- Independent save/content/generation/RNG/replay/telemetry versions.
- Stable `ContentId`, manifest-root confinement, duplicate/reference failure behavior.
- Current-only save compatibility, checksum, atomic replace, one backup, explicit diagnostics.
- Disabled/local-only telemetry with no network transport.
- MonoGame only at Game presentation and ContentBuilder tooling boundaries.

## Version and migration obligations

- Persisted shape or meaning: increment save schema and add a contiguous migration before release.
- Definition interpretation: increment content schema; compatible value/asset changes alter the catalog fingerprint.
- Seeded output or stream behavior: increment generation and/or RNG as applicable.
- Commands, initial state, tick order, or hash meaning: increment replay.
- Telemetry required fields/event meaning/units: increment telemetry.
- Scheduler order, dependency direction, numeric policy, ECS storage, save support policy, generation compatibility, or platform target: record an ADR and update architecture/deterministic tests.

## Required tests for extensions

Every extension supplies owner-level unit tests, insertion-order and render-cadence determinism where authoritative, stream-isolation tests where random, negative content validation, migration/golden-save tests where persisted, architecture checks, and a presentation/content smoke path where graphical.

## Prohibited shortcuts

Do not add a plugin loader, generic engine, reflection discovery, runtime CLR type names, mutable globals, service locator, behavior inheritance tree, raw ECS serialization, unseeded authoritative randomness, hidden callbacks, or a duplicate UI/domain rule.
