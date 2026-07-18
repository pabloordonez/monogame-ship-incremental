# P4_META_UI Package Specification

## Identity

- Package: `P4_META_UI`
- Branch: `phase2-p4-meta-ui`
- Pinned base: `0b12902972d5a98ff785c78a9e0c10728b2a2df0`
- Source: `docs/mvp/agent-workflow.md`, Phase 2 P4.

## Ownership

- Profile balances/counters/reward commits, research, capabilities, loadout/derived statistics, title/lobby/map/loadout/research/pause/summary UI, persistence expansion, settings, telemetry translations, and corresponding tests.
- Add package-owned files under Domain/Simulation/Persistence/Telemetry/Game/tests; P0 shared contracts and P1/P2/P3 paths are read-only.
- Missing reward, scheduler, command, profile, or event functionality requires a contract-change proposal and explicit version/migration analysis.

## Ordered delivery

1. Atomic/idempotent profile balances, counters, and accepted reward commits.
2. Twelve-node graph, purchases, grants, and capability-based Ion Veil access.
3. Five-slot loadout, compatibility, visible fallback diagnostics, and deterministic derived statistics.
4. Screen navigation, locks/explanations, previews, and summaries.
5. Complete versioned saves, migrations, settings, continue, backup, and recovery.
6. Local consent-aware telemetry that cannot affect play.

## Gates

- A new profile can complete the meta loop, purchase research, change loadout, save, and continue.
- All twelve nodes are acyclic/reachable and Ion Veil checks capability rather than research ID.
- Transactions are atomic/idempotent, balances nonnegative, and unknown IDs preserve recoverability.
- Golden/corrupt/newer/unknown/interrupted saves and telemetry schema/disable/failure/privacy tests pass.
- Package tests, architecture tests, full suite, headless smoke, and available graphical smoke pass with exact evidence.

## Evidence

Produce `implementer-report.md`, `reviewer-report.md`, `remediation-report.md`, `commands-and-results.txt`, `candidate-commit.txt`, `extension-note.md`, and `manual-evidence/`.
