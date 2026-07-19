# MVP Agent Workflow

## Objective

Deliver a maintainable vertical slice. The shipped loop is:

`new/continue -> Station -> loadout/research/upgrades -> select environment -> fly/fight/mine -> elite/extract -> rewards/save -> repeat`

P0–P5 package history below remains the delivery record; the vertical slice is integrated (`P5_INTEGRATION`). Ongoing work uses the same implement → adversarial review → remediation discipline.

Every package follows independent implementation, adversarial review, and remediation. A package is complete only after reviewer acceptance.

## Shared operating rules

- Work from the pinned repository commit in an isolated branch/worktree.
- Read this documentation set before editing.
- Stay inside owned paths and contracts; no unrelated cleanup or deferred features.
- Preserve deterministic fixed-step simulation and MonoGame-free domain logic.
- Use stable typed IDs, validated data, explicit scheduler order, and versioned persistence.
- Prefer ECS composition and small systems; no service locator, mutable globals, behavior inheritance trees, runtime type discovery, or speculative plugins.
- Add automated tests for behavior and regressions; graphical code also needs a manual smoke path.
- Never weaken a gate, hide a failure, or change requirements merely to pass.
- A contract change requires a short proposal naming affected packages and migration/version impact.
- Reports cite exact commands and results; “should pass” is not evidence.

## Repository evidence

Each package stores or links:

```text
package-spec.md
implementer-report.md
reviewer-report.md
remediation-report.md
commands-and-results.txt
manual-evidence/
candidate-commit.txt
extension-note.md
```

The extension note states what future additions are data-only, what needs a new component/system, stable contracts, and migration/test obligations.

## Phase 1 — Foundation package

One architecture implementer establishes the structure before parallel feature work.

Foundation is time-boxed to the smallest production implementation needed to unblock feature work. It defines one concrete path through each durable boundary; it does not generalize those boundaries, build unused adapters, or complete feature-owned persistence/telemetry/content behavior. Phase 2 expands the walking skeleton only when required by its vertical slices.

### `P0_FOUNDATION`

**Owns**

- Solution/project layout and dependency rules.
- Exact .NET/MonoGame 3.8.5/DesktopVK package pins.
- C# Content Builder console skeleton and one loadable manifest asset.
- Lightweight ECS stores, entity IDs, structural buffer, and explicit scheduler.
- Fixed-step host, neutral commands, owned PRNG/streams, state hashing.
- Application state shell: title, empty Station shell, empty run, summary transition.
- Content catalog/validation contracts.
- Versioned save envelope, migrations registry, atomic repository.
- Telemetry contract/local sink.
- Test projects, architecture tests, build/test/content scripts, CI baseline.
- One walking skeleton: launch an empty deterministic run, return, save, continue.

The migration registry may initially contain only the current schema and a tested unsupported-version result; historical migrations are added when a second released schema exists. Telemetry initially proves disabled/local-sink behavior, not a complete event catalog.

**Does not own**

Final gameplay, balance data, complete UI, production assets, enemies, mining, or research content.

**Gate**

- Clean restore/build/test/content-build/launch succeeds.
- DesktopVK window opens and walking skeleton completes.
- Identical seed/commands produce identical headless hashes under different render cadences.
- Gameplay projects have no MonoGame dependency.
- Save round trip, corruption recovery, and atomic write tests pass.
- Content duplicate/missing-ID validation fails correctly.
- No legacy MGCB source-of-truth exists.

### Foundation adversarial focus

Challenge dependency direction, ECS lifetime safety, system order, deterministic claims, RNG stream isolation, content validation, save corruption/path safety, resource disposal, over-engineering, and whether feature agents can work without sharing mutable implementation areas.

### Foundation remediation output

Publish a versioned contract baseline, system schedule, package map, and known limitations. Contracts are not permanently frozen: later changes require evidence, affected-owner approval, tests, and migrations/version increments where applicable.

## Phase 2 — Four bounded workstreams

Start only after `P0_FOUNDATION` is accepted. Each implementer takes its owned systems in the listed order and periodically rebases on accepted foundation/integration baselines.

### `P1_CONTENT_ART`

**Owned paths**

- `tools/ShipGame.ContentBuilder/`
- `content/source/`
- `content/definitions/`
- Asset/content validation tests.

**System order**

1. Asset manifest and provenance validator.
2. Runtime catalog generation and ID/reference validation.
3. MonoGame build rules and clean/incremental build.
4. Pixel-art contact sheet and palette.
5. Player/modules, enemies, asteroids/resources, environments, UI/icons, and essential SFX placeholders.
6. Atlas metadata, pivots, animations, collision references, and smoke loading.

**Gate**

- Every catalog/manifest ID resolves uniquely.
- All art matches grid/palette/pivot rules and remains readable in grayscale/effects-off.
- Clean and incremental content builds pass.
- Runtime loads all MVP asset IDs through the catalog.
- Provenance/licenses are complete.
- Every candidate or placeholder retained in a playtest build has a waiver recorded by the integration owner with asset ID, reason, replacement criterion, and license/provenance evidence.
- No music file is required; the commissioning brief remains the deliverable.

**Adversarial focus**

Legacy pipeline leakage, unstable path-based IDs, incorrect 3.8.5 API assumptions, bad alpha/pixel density, unreadable telegraphs, atlas bleeding, missing licenses, copied visual identity, and content that cannot be replaced independently.

### `P2_FLIGHT_COMBAT`

**Owned paths**

- Gameplay components/systems for commands, movement, spatial queries, combat, shields, abilities, AI.
- Game input adapters and combat presentation bindings.
- Corresponding tests and content behavior registrations.

**System order**

1. Keyboard/gamepad commands and ship movement.
2. Collisions, shield/hull, damage, and death.
3. Pulse, beam, seeker behavior and target locks.
4. Dash/blink and temporary/combat modifiers from station upgrades.
5. Interceptor, Gunship, Sapper, elite modifier, threat spawning.
6. Combat events, feedback bindings, and deterministic traces.

**Gate**

- All three weapon behaviors and enemies are exercisable.
- Movement/fire/shield/AI tests are exact and frame-rate independent.
- Keyboard/gamepad action parity passes.
- Stable simultaneous damage and entity-order tests pass.
- Combat runs at target performance without steady-state allocation growth.

**Adversarial focus**

Frame dependence, duplicate damage/rewards, unstable ordering, stale entities, collision tunneling, impossible seeker locks, shield boundary errors, inherited behavior duplication, input disparity, and presentation mutating simulation.

### `P3_WORLD_RUN`

**Owned paths**

- Encounter descriptors/generation/validation.
- Environment hazards, asteroid cells, mining, drops, collection.
- Objective, elite activation, extraction/failure, reward calculation.
- Corresponding presentation bindings and tests.
- Note: mid-run upgrade offers were superseded; station upgrades land in `P4_META_UI` / current meta catalogs.

**System order**

1. Versioned seeded field descriptor and reachability validation.
2. Cinder Belt and Ion Veil hazards.
3. Bounded asteroid cells, mining contacts, loot, and collection.
4. Combined objective and threat transitions.
5. Elite activation, extraction, timer/failure, and reward transaction proposal.

**Gate**

- 10,000 seeds per environment for the current generation version satisfy traversal, objectives, elite, extraction, and reward bounds (20,000 total for the MVP).
- RNG streams are isolated.
- Mining/resource conservation and collect-once tests pass.
- Success/failure produces one deterministic reward proposal with documented amounts; profile commit belongs to `P4_META_UI`.
- Same seed/content/input yields the same run events and result.

**Adversarial focus**

Invalid/unwinnable seeds, fallback nondeterminism, hidden wall-clock usage, duplicate loot, unreachable cells, objective deadlocks, extraction race conditions, timer/pause errors, and reward exploits.

### `P4_META_UI`

**Owned paths**

- Profile/resources/research/capabilities/loadout.
- Title/Station/map/loadout/research/upgrades/pause/summary UI.
- Persistence snapshots/migrations beyond foundation examples.
- Settings and validation telemetry translations.
- Corresponding tests.

**System order**

1. Profile balances, counters, atomic/idempotent commit of accepted run-reward proposals, and exactly-once protection.
2. Twelve-node research graph, purchases, grants, and Ion Veil gate.
3. Twelve station upgrades with banked costs, purchase persistence, and run-start modifier fold.
4. Five-slot loadout, validation, fallbacks, and derived statistics.
5. Screen navigation, explanations, stat previews, and summaries.
6. Complete saves/migrations/settings/continue.
7. Local telemetry events and consent/disable behavior.

**Gate**

- New profile can complete the loop, buy research or a station upgrade, change loadout, save, and continue.
- All twelve research nodes have valid costs/dependencies/gates and are reachable.
- All twelve station upgrades are purchasable once and apply on the next launch.
- Capability—not research-ID—access checks unlock Ion Veil.
- Transactions are atomic/idempotent and balances never go negative.
- Golden saves, corrupt saves, unknown IDs, and interrupted writes pass.
- Telemetry supplies the documented quantitative evidence and cannot block play; observations, interviews, and surveys supply qualitative evidence.

**Adversarial focus**

Research cycles/dead ends, unaffordable progression, ID coupling, negative/duplicate transactions, save loss, silent fallback, migration gaps, UI/domain duplication, unclear locks, telemetry PII/volume, and inaccessible navigation.

## Parallel integration policy

- Foundation publishes interfaces and test doubles before Phase 2.
- Workstreams own implementations; cross-workstream calls use accepted contracts.
- Shared contract files have one named owner from `P0_FOUNDATION`.
- A proposed contract change includes motivation, old/new shape, impacted packages, version/migration effect, and compatibility tests.
- Integrate in thin slices: movement shell, combat arena, mining objective, rewards/progression, then complete presentation.
- Rebase and run full tests after each accepted package merge.
- Never resolve a conflict by duplicating domain rules in an adapter or UI.

## Phase 3 — Integration package

### `P5_INTEGRATION`

**Owns**

- Assemble accepted packages through their contracts.
- Remove duplicate integration logic.
- Complete fresh-profile and continue paths.
- Golden input traces, balancing configuration, release packaging, notices, known issues, and playtest build.

**Gate**

- Full loop completes twice: fresh and continued profile.
- No debug commands, save edits, or seed substitutions are required.
- Deterministic trace reaches expected checkpoints.
- All automated, content, architecture, migration, and smoke suites pass.
- Performance/reliability gates pass, **or** failures are filed with traces and severity (P5 left the 1080p ten-minute capture and 50-run marathon outstanding for Round A; see `docs/reports/P5_INTEGRATION/known-issues.md`).
- Asset credits match provenance.
- Playtest checklist and local telemetry are usable.

**Adversarial focus**

Cross-system timing, ownership duplication, contract erosion, content/code mismatch, save compatibility, lifecycle leaks, performance spikes, corrupted-profile recovery, keyboard/gamepad end-to-end paths, missing credits, and undocumented release assumptions.

## Mandatory three-agent protocol

Use this protocol for `P0` through `P5`.

### Implementer prompt

> You are the implementer for `{PACKAGE_ID}: {PACKAGE_NAME}` in the Ship Game MVP.
>
> Inputs: base commit, immutable package scope/gates, accepted contracts, relevant MVP documents, owned paths, required commands, and known exclusions.
>
> Implement only this package. Preserve deterministic fixed-step simulation, ECS composition, stable IDs, validated data, and headless tests. Do not add deferred features or unrelated cleanup. If an accepted contract must change, stop and produce a contract-change proposal naming affected owners, compatibility, versions, migrations, and tests.
>
> Run all package checks. Return: assumptions; requirement-to-change mapping; changed files; contracts added/changed; tests and exact results; manual evidence; risks/deferred items; extension note; candidate commit/diff. Output `IMPLEMENTATION_BLOCKED` if any gate lacks evidence.

Implementer output is rejected if it lacks a gate mapping, contains unrelated changes, proceeds with an unstated contract change, or reports unexecuted tests as passing.

### Adversarial reviewer prompt

> You are the independent adversarial reviewer for `{PACKAGE_ID}: {PACKAGE_NAME}`. Do not edit files. Assume the candidate may compile while violating behavior, determinism, architecture, safety, licensing, or scope.
>
> Inputs: immutable package spec, base/candidate diff, implementer report, accepted contracts, commands, and gates.
>
> Inspect the diff and execute relevant checks. Attempt to falsify every acceptance claim. Check failure paths, frame dependence, ordering, save/content compatibility, resource lifetime, input parity, data validation, asset provenance, telemetry privacy, performance, test quality, hidden coupling, duplication, and extension obligations as applicable.
>
> Return: gate-by-gate verdict with evidence; findings ordered `critical`, `major`, `minor`; exact reproductions; missing/misleading tests; downstream impact; verdict `ACCEPT`, `REMEDIATE`, or `BLOCK`.

`ACCEPT` requires evidence for every gate and no unfiled critical/major finding. Filed majors with traces (for example outstanding perf/reliability captures) may leave Round A evidence incomplete without rewriting the package as failed. Style preference alone cannot block. `BLOCK` is for invalid assumptions or unresolved contract decisions.

### Remediation prompt

> You are the remediation agent for `{PACKAGE_ID}: {PACKAGE_NAME}`.
>
> Inputs: original package spec, candidate diff, implementer report, reviewer report, accepted contracts, and commands.
>
> Reproduce each critical/major finding or disprove its reproduction with evidence. Apply the smallest durable correction, add a regression test, and rerun package and affected full-suite gates. Do not broaden scope, weaken assertions, suppress failures, or mark required work deferred merely to pass.
>
> Return: finding-to-fix mapping; files/rationale; regression tests; exact results; remaining/disputed findings; updated commit/diff; `READY_FOR_RECHECK` or `REMEDIATION_BLOCKED`.

The independent reviewer rechecks remediation. Remediation cannot self-accept.

## Severity and completion

- **Critical:** data loss, security boundary violation, crash/blocker, deterministic corruption, impossible core loop.
- **Major:** wrong required behavior, architecture boundary breach, material performance/accessibility failure, missing migration/test.
- **Minor:** bounded maintainability, polish, or low-impact issue.

A package completes only with `ACCEPT`. A phase completes only after all packages in it are accepted and the phase-boundary integration suite passes.
