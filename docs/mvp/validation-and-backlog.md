# MVP Validation and Backlog

## Objective

Determine whether the smallest complete expedition loop is understandable, satisfying, repeatable, low-pressure, and meaningfully progressive. The MVP does not validate galaxy-scale simulation, automation, factions, or a large content catalog.

Thresholds are project decisions, not industry benchmarks. Preserve sample size, raw observations, build/content versions, and negative results.

## Automated test layers

### Unit/property tests on every change

- ECS entity reuse, sparse stores, structural buffers, and queries.
- Fixed-step movement, collision pairs, shield/hull boundaries, fire cadence, heat, targeting, and abilities.
- Mining yield, collect-once, drop determinism, upgrade thresholds/offers/effects.
- Profile transactions, research dependencies/gates, capabilities, loadout, and derived statistics.
- RNG golden sequences and stream isolation.
- Generation reachability/resources/objective/extraction invariants.
- Save serialization, compatibility, migration, validation, and interrupted writes.
- Content/manifest schemas, IDs, references, graphs, atlas regions, provenance, and hashes.
- Telemetry schema, aggregation, disabled mode, and sink failure.

### Deterministic simulation tests

Maintain at least these versioned traces:

1. Basic flight, dash, pause, and return.
2. Each weapon against shields/hull and all enemy archetypes.
3. Mine, collect, trigger upgrades, complete objective, defeat elite, and extract.
4. Fail by hull and by timer.
5. Resolve rewards, purchase research, equip a module, save, and continue.

Run traces under different render-frame schedules and component insertion orders. Compare ordered events and stable domain state at checkpoints, not rendered pixels.

### Generation tests

- Validate at least 10,000 seeds per supported generation/environment combination.
- Ensure spawn, three objective sectors, elite arena, extraction, corridors, required Ferrite, and legal spawn anchors.
- Check duration/reward distributions against configured bounds.
- Verify fallback generation is deterministic.
- Verify an extra roll in one RNG stream cannot change another stream.
- Preserve golden descriptors for representative seeds and every supported generation version.

### Integration/smoke tests

- Clean content build then load every manifest/catalog ID.
- New profile through extraction, research, loadout change, save, and continue.
- Failure resolution and recovery.
- Keyboard/mouse and gamepad golden paths.
- Options persistence and controller reconnect.
- Corrupt-save quarantine, backup recovery, unknown ID, and newer-version rejection.
- Clean shutdown from lobby, pause, upgrade offer, and summary.
- Credits/attribution match the manifest.
- Game remains playable with music absent and effects disabled.

### Architecture tests

Enforce all rules in [technical-architecture.md](technical-architecture.md), including project dependency direction, MonoGame isolation, component restrictions, system registration/order, version declarations, migrations, typed content IDs, seeded RNG, and headless simulation.

## Manual release-candidate checks

- Clean supported Windows machine or VM with Vulkan-capable driver.
- 720p, 1080p, windowed, fullscreen, and non-integer window sizes.
- Keyboard/mouse and common XInput-compatible controller.
- Disconnect/reconnect, focus loss, pause, resize, and exit.
- Bloom, particles, shake, flashes, vibration, SFX, and UI sound independently disabled where applicable.
- Grayscale and common color-vision simulations.
- Threat, objective, lock, resource, elite, and extraction readability.
- Twenty-minute session for repetition, visual fatigue, and lifecycle leaks.
- Replace one atlas asset without gameplay/data changes.
- Load a prior supported save after a content-only update.

## Performance and reliability gates

Measure release builds after a two-minute warm-up on the MVP reference class:

- Windows 10/11 x64, four physical CPU cores at 3.0 GHz or better, 8 GB RAM, SSD, and a Vulkan 1.2 GPU with 2 GB dedicated/shared graphics memory.
- Record exact CPU, GPU, RAM, OS build, driver, resolution, build/content version, and capture tool with every result; choose and retain one physical reference machine before the first performance baseline.
- Gameplay sustains 60 fixed updates/second with no accumulated tick debt during the busiest encounter.
- Rendering targets 60 frames/second at 1080p: median frame time no greater than 16.7 ms, no more than 1% of frames above 16.7 ms, and 99th-percentile frame time no greater than 33.3 ms over a ten-minute capture.
- No sustained managed allocation in steady gameplay; document unavoidable transients.
- No unbounded working-set/entity/event/particle growth over three expeditions.
- Startup to interactive title within five seconds on SSD.
- Save transaction within 250 ms locally.
- No crash, hang, duplicated reward, or save loss in 50 automated golden-path runs.

A missed gate requires a trace and owned finding; subjective playability does not waive it.

## Telemetry

Development/playtest telemetry writes versioned local JSONL. External transmission is not part of the MVP and would require consent, retention policy, endpoint/security review, and deletion/reset controls.

Common fields:

- `schemaVersion`, `eventName`, `utcTimestamp`.
- Resettable random install ID, session ID, run ID.
- Build/content/generation versions.
- Input mode, environment ID, seed, and monotonic elapsed time.
- Event-specific payload.

Required events:

- Session/title/lobby/run/summary entry and exit.
- New/continue, environment selected, lock inspected, run started/resolved.
- First use of move/aim/fire/mine/mobility/interact.
- Aggregated damage, shield depletion, player/normal/elite destruction.
- Mining, resource collection, upgrade offer/selection.
- Objective completion, extraction start/reset/completion.
- Research view/purchase/rejection and loadout change.
- Save start/success/failure/recovery.
- Option change, performance sample, and unhandled error.

Do not collect names, email, OS usernames, IP addresses, hardware fingerprints, raw text, raw control samples, or save snapshots. Aggregate high-frequency damage and sample performance every ten seconds/boundary. Telemetry failure never affects play.

## Playtest rounds

### Round A — internal removal of blockers

- Five participants.
- Validate build, controls, telemetry, objective comprehension, and interview script.
- Results tune obvious defects but do not count as target-player evidence.

### Round B — first target-context test

- Eight to twelve people who enjoy low-attention, incremental, extraction, or roguelite games.
- No tutorial beyond: “Play until you feel you have completed a useful trip.”
- Observe the first run silently.
- Ask them to inspect research, change something if desired, and continue until they naturally stop.
- Record 1–7 ratings for control clarity, pressure, progress, upgrade meaning, and desire for another run.
- Interview for memorable moments, confusion, failure cause, strategy, and session-end reason.

### Round C — evidence-backed iteration

- At least eight target-context participants after material changes.
- Separate first-time comprehension from repeat-participant progression evidence.
- Use the same core questions and version all additions.

Small samples support directional decisions, not population claims.

## Hypotheses and decisions

### H1 — Understandable unaided

Pass when at least 70% launch without help, median launch time is under three minutes, at least 70% complete both mining and combat actions, and no required action blocks over 20%.

If failed, improve affordances, labels, feedback, or controls before adding content.

### H2 — Satisfying immediate play

Pass when median control rating is at least 5/7, fewer than 10% of failures are blamed on unreadable damage/unclear rules, and qualitative notes identify responsive movement, firing, mining, or collection.

If failed, tune control curves, feedback, telegraphs, and pacing before progression.

### H3 — Meaningful temporary and permanent growth

Pass when at least 60% name a meaningful run-upgrade choice, at least 70% correctly describe their first research effect, at least 50% change loadout after an unlock, and effects produce measured differences.

If failed, revise effects, costs, previews, or run yield before expanding catalogs.

### H4 — Replay desire

Pass when at least 60% voluntarily begin a fourth run or explicitly ask to continue, median “one more run” rating is at least 5/7, and later runs express distinct goals.

If failed, inspect pacing, choices, and progression before adding procedural variety.

### H5 — Fair variation and gates

Pass when all generation invariants hold, reward/duration distributions stay bounded, players notice variation without median unfairness above 2/7, and at least 70% can explain the Ion Veil requirement after inspecting it.

If failed, constrain generation, guarantees, and gate communication before adding environments.

### H6 — Low-attention compatibility

Pass when median pressure is 2–5/7, at least 70% report they could listen to spoken audio while playing, pause is found immediately, and telegraphed failures are understandable.

Do not simulate an interruption while the game is unpaused and call resulting deaths a defect; the intended safety mechanism is immediate pause.

## MVP exit gate

Proceed to post-MVP layers only when:

- Critical technical/reliability/accessibility gates pass.
- H1, H2, and H3 pass.
- H4–H6 pass, or an explicit pivot/accepted-risk decision records the failed evidence, owner, player impact, and reason the MVP should proceed. Merely planning another iteration is not a pass.
- Generation invariants pass.
- No unresolved save loss, unreadable mandatory cue, inaccessible required control, or deterministic defect remains.
- Evidence identifies which mechanic drives replay intent.

Stop or pivot when two post-remediation rounds fail meaningful growth or replay desire. Preserve the reusable architecture and document the failed hypothesis instead of masking it with more content.

## Deferred backlog

### Evidence-selected next experiments

- One additional weapon or specialization.
- More enemy compositions/objectives.
- A third environment/resource tier with another capability gate.
- Expanded research with real trade-offs.
- Richer dash/light-speed attack.
- Orbiting helper specialization.
- Start-screen parallax and presentation polish.
- Better bloom/lighting and commissioned music.

Each candidate needs a player problem, hypothesis, bounded implementation, test, migration/version assessment, and removal criterion.

### Later product layers

- Connected procedural galaxy and broader star taxonomy.
- Fuel and restocking.
- Trading/economy.
- Alien races, diplomacy, and reputation.
- Planets, orbit, probes, and persistent facilities.
- Passive income, production chains, automation, routes, and defense.
- Gravity, black holes, and galaxy-core progression.
- Fleets/multiple hulls.
- Procedural planets/moons/nebula families and rare events.
- Narrative/cinematics, cloud saves, and cross-platform releases.

### Explicitly deferred technical work

- General-purpose destructible terrain.
- Networking, accounts, backend analytics, and cloud synchronization.
- Public mods/plugins and content-authoring tools.
- Generic scripting/effect graph/editor.
- Multithreaded ECS or universal physics.
- Custom content processor when validated JSON is sufficient.
- Platform optimization before another platform is selected.

## Priority rule

1. Reliability, accessibility, and comprehension defects.
2. Changes needed to test a failed core hypothesis.
3. Features that deepen a proven loop.
4. Content variety.
5. Presentation polish.
6. Speculative systems.
