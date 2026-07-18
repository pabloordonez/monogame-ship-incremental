# P2_FLIGHT_COMBAT Package Specification

## Identity

- Package: `P2_FLIGHT_COMBAT`
- Branch: `phase2-p2-flight-combat`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Source: `docs/mvp/agent-workflow.md`, Phase 2 P2.

## Ownership

- Flight commands, movement, spatial queries, collisions, combat, shields, hull, abilities, enemies, AI, threat spawning, input adapters, combat feedback, behavior registrations, and corresponding tests.
- Add package-owned files under Simulation/Game/tests; P0 shared foundation contracts and P1/P3/P4 paths are read-only.
- Missing shared functionality requires a contract-change proposal rather than duplicated rules or adapters.

## Ordered delivery

1. Keyboard/gamepad command parity and movement.
2. Collision, shield/hull, damage, death, and stable ordering.
3. Pulse, beam, seeker, target lock, cadence, and heat.
4. Dash/blink and temporary combat modifier consumption.
5. Interceptor, Gunship, Sapper, elite modifier, and threat spawning.
6. Immutable combat events, read-only feedback bindings, deterministic traces, and allocation/performance evidence.

## Gates

- All weapons and enemies are exercisable with exact frame-rate-independent tests.
- Keyboard/gamepad action parity, stable simultaneous damage/entity order, shield boundaries, stale entities, target loss, collision mobility cases, and presentation isolation pass.
- Combat sustains the target fixed step without steady-state allocation growth, with recorded measurement limits.
- Package tests, architecture tests, full suite, headless smoke, and available graphical smoke pass with exact evidence.

## Evidence

Produce `implementer-report.md`, `reviewer-report.md`, `remediation-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, and `manual-evidence/`.
