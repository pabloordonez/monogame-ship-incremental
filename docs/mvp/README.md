# Ship Game MVP Documentation

This directory defines the first production vertical slice of Ship Game (player-facing title: **Mine Your Own Business**). Its purpose is to test whether flying, fighting, mining, buying station upgrades, extracting, and buying permanent research creates an enjoyable low-pressure loop.

## Document map

1. [MVP charter](mvp-charter.md) — purpose, hypotheses, scope, and exit criteria.
2. [Game design](game-design.md) — player flow and exact gameplay rules.
3. [Content catalog](content-catalog.md) — stable IDs and initial balance for all MVP content.
4. [Systems](systems.md) — ownership, contracts, ordering, and system acceptance tests.
5. [Technical architecture](technical-architecture.md) — solution structure, ECS, determinism, data, persistence, and quality constraints.
6. [Evolution strategy](evolution-strategy.md) — how later layers extend the MVP without speculative infrastructure.
7. [Art and audio direction](art-audio-direction.md) — visual language, asset manifest, Content Builder rules, and music brief.
8. [Agent workflow](agent-workflow.md) — phased implementation with implement, adversarial review, and remediation passes.
9. [Validation and backlog](validation-and-backlog.md) — tests, playtests, telemetry, exit gates, and deferred work.

## Code project guides

The design docs above remain the product source of truth for intended MVP behavior, and must stay aligned with the current implementation. For day to day code work, each `src` project has a README that explains ownership, folder layout, and how to extend that layer. Start with [ShipGame.Gameplay](../../src/ShipGame.Gameplay/README.md) when adding weapons, enemies, movement changes, research, station upgrades, or world-run side effects. Use [ShipGame.Game](../../src/ShipGame.Game/README.md) for screens, input, and presentation bindings. The thinner rings are [Domain](../../src/ShipGame.Domain/README.md), [Ecs](../../src/ShipGame.Ecs/README.md), [Content](../../src/ShipGame.Content/README.md), [Persistence](../../src/ShipGame.Persistence/README.md), and [Telemetry](../../src/ShipGame.Telemetry/README.md).

## Source-of-truth order

When documents disagree, use this order:

1. [`docs/requirement.md`](../requirement.md) defines the long-term product intent.
2. This README and [MVP charter](mvp-charter.md) define the approved MVP boundary.
3. [Game design](game-design.md) defines player-facing behavior.
4. [Content catalog](content-catalog.md) defines IDs, content relationships, and tuning notes (runtime registries win for combat stats where noted).
5. [Systems](systems.md) and [technical architecture](technical-architecture.md) define implementation boundaries.
6. [Agent workflow](agent-workflow.md) defines delivery procedure, not product behavior.

When living docs disagree with the current code, update the living docs. Resolve a real product ambiguity by recording a decision before changing code. Do not silently choose whichever document is convenient.

Phase delivery evidence under [`docs/reports/`](../reports/) is archival. Those packages may describe older project names (`ShipGame.Simulation`), mid-run upgrade offers, or Lobby wording that the current MVP no longer ships.

## Fixed MVP decisions

- Platform: Windows desktop.
- Framework: MonoGame 3.8.5 DesktopVK and its code-centric C# Content Builder.
- Session: 10–15 minutes including Station decisions.
- Ship: one hull, the Wayfarer Mk I.
- Run: a deterministic seeded field with combat, bounded asteroid destruction, mining, an elite, and extraction.
- Progression: twelve permanent station-purchased run upgrades and twelve permanent research nodes.
- Access test: the second environment is visible but capability-gated.
- Persistence: one local profile with versioned, recoverable saves.
- Art: replaceable pixel-art assets referenced by stable IDs.
- Music deliverable: design brief only; no generated or placeholder music is required.
- Product title: Mine Your Own Business (window/content); repository name Ship Game.

## Working vocabulary

- **Run/expedition:** one launch through extraction or failure.
- **Environment:** a star-field ruleset, hazards, palette, and reward weighting.
- **Module:** persistent equipment selected at Station.
- **Station upgrade:** permanent profile purchase that folds into run modifiers at launch.
- **Research:** permanent profile unlock or modifier.
- **Capability:** semantic access permission granted by research or equipment; gates query capabilities, not research IDs.
- **Content ID:** stable serialization key independent of display name or file path.
- **Gameplay:** authoritative deterministic gameplay state.
- **Presentation:** input adapters, rendering, audio, particles, and UI around the simulation.
- **Station:** meta hub between Title/Summary and Map/Loadout/Research/Upgrades/Settings (replaces earlier “Lobby” wording).

## Change policy

This is a durable base, not a disposable prototype. New mechanics enter as vertical slices with an owner, scheduler position, data IDs, tests, presentation binding, version impact, and migration plan where needed. Do not build a plugin framework, generic engine, or unused subsystem in anticipation of deferred features.
