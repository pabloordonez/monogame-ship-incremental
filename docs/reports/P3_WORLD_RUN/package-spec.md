# P3_WORLD_RUN Package Specification

## Identity

- Package: `P3_WORLD_RUN`
- Branch: `phase2-p3-world-run`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Source: `docs/mvp/agent-workflow.md`, Phase 2 P3.

## Ownership

- Generation/descriptors/validation, hazards, asteroid cells, mining, loot, collection, objectives, upgrades, elite activation, extraction, failure, deterministic reward proposals, presentation bindings, and corresponding tests.
- Add package-owned files under Simulation/Game/tests; profile reward commit belongs to P4 and P0/P1/P2/P4 paths are read-only.
- Missing collision, destruction, event, scheduler, or reward handoff contracts require proposals, not duplicate implementations.

## Ordered delivery

1. Versioned deterministic field descriptor and reachability validator.
2. Cinder Belt and Ion Veil hazards.
3. Bounded cells, mining contacts, deterministic loot, collection, and conservation.
4. Combined objective and threat/elite transitions.
5. Charge thresholds, four deterministic non-repeating offers, and temporary effects.
6. Elite activation, extraction, timer/failure ordering, and exactly-once reward proposal.

## Gates

- 10,000 seeds per environment pass traversal, objective, elite, extraction, fallback, and reward invariants.
- RNG streams remain isolated; same seed/content/input produces identical events/results.
- Mining conservation, collect-once, objective, four-offer, pause/timer, extraction race, and exactly-once reward tests pass.
- Package tests, architecture tests, full suite, headless smoke, and available graphical smoke pass with exact evidence.

## Evidence

Produce `implementer-report.md`, `reviewer-report.md`, `remediation-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, and `manual-evidence/`.
