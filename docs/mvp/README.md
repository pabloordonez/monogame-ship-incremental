# Ship Game MVP Documentation

This directory defines the first production vertical slice of Ship Game. Its purpose is to test whether flying, fighting, mining, choosing temporary upgrades, extracting, and buying permanent research creates an enjoyable low-pressure loop.

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

## Source-of-truth order

When documents disagree, use this order:

1. [`docs/requirement.md`](../requirement.md) defines the long-term product intent.
2. This README and [MVP charter](mvp-charter.md) define the approved MVP boundary.
3. [Game design](game-design.md) defines player-facing behavior.
4. [Content catalog](content-catalog.md) defines IDs, content relationships, and initial tuning.
5. [Systems](systems.md) and [technical architecture](technical-architecture.md) define implementation boundaries.
6. [Agent workflow](agent-workflow.md) defines delivery procedure, not product behavior.

Resolve a real ambiguity by recording a decision before implementation. Do not silently choose whichever document is convenient.

## Fixed MVP decisions

- Platform: Windows desktop.
- Framework: MonoGame 3.8.5 DesktopVK and its code-centric C# Content Builder.
- Session: 10–15 minutes including lobby decisions.
- Ship: one hull, the Wayfarer Mk I.
- Run: a deterministic seeded field with combat, bounded asteroid destruction, mining, an elite, and extraction.
- Progression: twelve temporary run upgrades and twelve permanent research nodes.
- Access test: the second environment is visible but capability-gated.
- Persistence: one local profile with versioned, recoverable saves.
- Art: replaceable pixel-art assets referenced by stable IDs.
- Music deliverable: design brief only; no generated or placeholder music is required.

## Working vocabulary

- **Run/expedition:** one launch through extraction or failure.
- **Environment:** a star-field ruleset, hazards, palette, and reward weighting.
- **Module:** persistent equipment selected in the lobby.
- **Run upgrade:** temporary choice removed when a run ends.
- **Research:** permanent profile unlock or modifier.
- **Capability:** semantic access permission granted by research or equipment; gates query capabilities, not research IDs.
- **Content ID:** stable serialization key independent of display name or file path.
- **Simulation:** authoritative deterministic gameplay state.
- **Presentation:** input adapters, rendering, audio, particles, and UI around the simulation.

## Change policy

This is a durable base, not a disposable prototype. New mechanics enter as vertical slices with an owner, scheduler position, data IDs, tests, presentation binding, version impact, and migration plan where needed. Do not build a plugin framework, generic engine, or unused subsystem in anticipation of deferred features.
