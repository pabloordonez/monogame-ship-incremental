# P0_FOUNDATION Remediation Report

## Status and identity

`READY_FOR_RECHECK`

- Pinned base: `a443f89c920e37b2d46385a1d18b2e34efe4892f`
- Original candidate: `38f2db9b429533653883a3d031299359b5d9fe7e`
- Original evidence: `2bce17f86019b3da471c9259af44db3638032b95`
- Implementation remediation: `5523b94ef2fb9cbb153e65c94d1c2e7671a5fc46`
- Remediation range: `2bce17f86019b3da471c9259af44db3638032b95..5523b94ef2fb9cbb153e65c94d1c2e7671a5fc46`

This report does not accept the package. Independent recheck is required.

## Finding-to-fix mapping

### C1 — malformed persistence members

- `PersistenceContracts.cs` validates the JSON root, all durable-version fields, profile fields, and required strings before DTO dereference.
- Null/missing/mistyped required members return `Corrupt`; primary corruption attempts known-good backup recovery.
- Persistence regressions cover null and missing `Versions` in primary and in both primary/backup.

### M1 — unsafe raw ECS mutation

- `ComponentStore<T>` and raw mutation are internal. Public `IComponentView<T>` is read/query-only.
- `World` owns generation-validated set/remove/create/destroy operations.
- Active query enumeration blocks structural mutation and directs callers to `CommandBuffer`.
- Tests cover stale IDs, reuse, read-only views, query-time mutation, buffered mutation, insertion order, and 1,000 randomized lifecycle operations.

### M2 — incomplete persistence compatibility

- `SaveMigrationRegistry` classifies save, content, generation, RNG, replay, and telemetry versions plus expected catalog fingerprint.
- Content schema/fingerprint mismatches return `MissingContent`; newer unsupported authority versions return `IncompatibleNewer`; malformed/unsupported older state returns `Corrupt`.
- Checksum validation runs before compatibility classification.
- Independent regressions exercise every durable dimension and catalog mismatch.

### M3 — Continue retains default RNG identity

- `ShipGameHost` reconstructs `FoundationSimulation` and `FixedStepDriver` from the loaded profile before issuing Continue.
- Invalid/incompatible Continue no longer silently advances a default profile.
- Foundation run seed derivation includes profile seed, run index, and generation version.
- Headless smoke reloads the save, reconstructs the continued run, and asserts saved seed, run index, and first Encounter RNG output end to end.

### M4 — telemetry privacy and isolation

- Telemetry records can only be created through validation that accepts bounded scalar values.
- Canonical event/field names, field count, estimated payload size, finite numerics, and prohibited PII/raw-data field fragments are enforced; string/object/cyclic payloads are rejected.
- Local sink construction, serialization/write/flush, post-disposal writes, and disposal exceptions are contained and reflected by `Failed`.
- Tests cover disabled behavior, valid JSONL, PII, raw strings, cyclic dictionaries, unsupported objects, field/size limits, construction failure, and write-after-disposal failure.

### M5 — pending commands omitted from hashes

- Authoritative hash now includes profile seed, run index, tick, app/run state, run signature, consumed command flags, all RNG states, and every pending command field.
- Pending commands are hashed in sorted target-tick order.
- Regressions prove queued-vs-empty divergence, insertion-order stability, and sensitivity of every command field.
- This corrects the unreleased version-1 replay/hash contract; no released save/replay exists to migrate.

### M6 — hardcoded Content Builder inputs

- `ContentBuildPlan` derives copied P0 data sources from the validated manifest in stable ID order.
- The MonoGame 3.8.5 code-centric builder consumes the plan; no source filename is hardcoded except the authoritative manifest itself.
- Unknown compiled media kinds fail until an owning package adds a reviewed type/folder rule.
- Regression proves new valid data entries enter the build plan without builder edits. Clean and incremental builds pass.

### M7 — untyped/empty definition IDs

- `ContentDefinition.Id` and `References` now use `ContentId`.
- Construction rejects empty/whitespace IDs before definitions enter runtime content.
- Tests cover typed exposure, valid references, empty IDs, and missing typed references.

### M8 — incomplete architecture gates

- Tests inspect all seven production assemblies and enforce the allowed dependency graph, cycle freedom, MonoGame isolation, authoritative forbidden APIs/unseeded RNG, data-only value types, explicit stable schedule, typed content IDs, all durable declarations, current migration continuity, and headless authority initialization.
- Synthetic cycle, dependency, forbidden API, migration gap, and delegate-component violations demonstrate that policy helpers reject representative bad inputs.

### m1 — command-buffer failure replay

- Commands are removed immediately before execution. Completed and throwing commands are consumed; unattempted commands remain ordered for a later synchronization attempt.
- Regression proves completed commands do not replay after a throw.

### m2 — null/missing manifest assets

- Null or missing `assets` now produces `ContentValidationException` with `manifest.assets-required`.
- Both forms have regression coverage.

## Files and rationale

- `src/ShipGame.Persistence/PersistenceContracts.cs`: safe DTO validation and complete compatibility.
- `src/ShipGame.Ecs/EcsWorld.cs`: world-owned mutation, query phase enforcement, deterministic command failure behavior.
- `src/ShipGame.Game/ShipGameHost.cs`: continued deterministic identity and end-to-end smoke.
- `src/ShipGame.Simulation/FoundationSimulation.cs`: run identity derivation and complete pending-command hash.
- `src/ShipGame.Telemetry/TelemetryContracts.cs`: bounded payload and sink isolation.
- `src/ShipGame.Content/ContentContracts.cs`, `tools/ShipGame.ContentBuilder/Program.cs`: typed definitions, malformed-manifest handling, manifest-driven data build plan.
- `tests/*`: focused regressions and expanded architecture fitness gates.
- `docs/reports/P0_FOUNDATION/reviewer-report.md`: reviewer result preserved with `REMEDIATE`.
- `docs/reports/P0_FOUNDATION/extension-note.md`: accurately limits automatic data-only additions to supported `kind: data` entries.

## Regression and full command results

- `dotnet build ShipGame.sln --configuration Release`: exit 0; 0 warnings, 0 errors.
- Initial `dotnet test ... --no-build --no-restore`: exposed one test-reflection mistake; 57 passed and 1 failed. The test was corrected without weakening the production assertion.
- `dotnet test ShipGame.sln --configuration Release`: exit 0; 58 passed, 0 failed, 0 skipped.
- `powershell.exe ... scripts/test.ps1`: exit 0; restore/build/content succeeded; 58 passed, 0 failed, 0 skipped; 0 build warnings/errors.
- `powershell.exe ... scripts/launch.ps1 -Smoke`: exit 0; rebuilt content/solution and completed saved-profile Continue identity assertions.
- DesktopVK `--window-smoke`: exit 0; Vulkan/Win32 surface, content-ready, walking-skeleton-complete, RTX 3090 selection, and swapchain recreation observed.
- Incremental Content Builder: exit 0; 2 succeeded, 0 failed.
- Package listing: exit 0; runtime and builder MonoGame packages resolved exactly to 3.8.5.
- Legacy pipeline scan: exit 0; no prohibited match.
- `git diff --check`: exit 0.
- IDE diagnostics: no linter errors.

Exact commands and suite counts are recorded in `commands-and-results.txt`.

## Remaining or disputed findings

None. C1, M1–M8, m1, and m2 were reproduced and corrected. No finding was deferred or disputed.

## Risks

- P0 telemetry intentionally permits only bounded numeric/boolean/null payload values. Later semantic string dimensions require an explicit allowlisted typed representation and telemetry-version review.
- P0 Content Builder automatically copies validated data entries only. Compiled media rules remain owned by the later content package and fail closed today.
- Architecture source-policy checks are deliberately narrow and explicit; new approved authoritative APIs require updating the documented policy and its representative violation tests.
- The independent reviewer must re-run adversarial checks; remediation cannot self-accept.
