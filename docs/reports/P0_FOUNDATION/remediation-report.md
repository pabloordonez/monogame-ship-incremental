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

## Second remediation pass — final acceptance recheck

### Status and identity

`READY_FOR_RECHECK`

- Rechecked evidence baseline: `9575c1adb295ca7616dbcc0128e50bc0bd686c32`
- Prior remediation implementation: `5523b94ef2fb9cbb153e65c94d1c2e7671a5fc46`
- Second remediation implementation: `3d59cbe3f6e7e8d0230fb58c44c82999a7637f7a`
- Second implementation range: `9575c1adb295ca7616dbcc0128e50bc0bd686c32..3d59cbe3f6e7e8d0230fb58c44c82999a7637f7a`
- Recheck verdict preserved in `reviewer-report.md`: `REMEDIATE`

This second pass does not accept the package. It is submitted for another independent recheck.

### M1 — mutable ECS entity list

- Reproduced: `IComponentView<T>.Entities` returned the live `List<EntityId>`, which was castable to mutable list interfaces.
- Fix: each access now returns a detached `EntitySnapshot` implementing only `IReadOnlyList<EntityId>`; it exposes no mutable collection interface and cannot alter sparse/dense backing storage.
- Regression: runtime casts to generic and non-generic mutable list interfaces fail. Removing a component after snapshot creation proves the snapshot is detached while store count, `Has`, `Read`, and `Get` remain aligned and valid.

### M4 — mutable telemetry payload

- Reproduced: `TelemetryRecord.Payload` returned the sanitized `Dictionary` behind an interface and remained castable to mutable dictionary interfaces.
- Fix: the record copies values into a private `FrozenDictionary`, then exposes it only through a private wrapper implementing `IReadOnlyDictionary`. Accepted values remain scalar immutable values; nested mutable structures are rejected before construction.
- Regression: mutable dictionary casts fail, mutation of the original source after record construction does not alter the record, and persisted JSONL retains the validated numeric value without the injected email/string.
- Existing construction, write, disposal, unsupported-type, cycle, PII, raw-string, size, and disabled-sink isolation tests continue to pass.

### M8 — project architecture policy did not inspect project files

- Reproduced: the prior test inferred only emitted assembly references, omitted `ShipGame.ContentBuilder`, and tested a separate hand-built graph.
- Fix: `ProjectArchitecturePolicy` loads actual production `.csproj` XML from `src` and `tools/ShipGame.ContentBuilder` using normalized platform paths. It validates all eight required projects, allowed `ProjectReference` edges, unknown/missing/duplicate references, cycle freedom, allowed `PackageReference` placement, and exact MonoGame 3.8.5 package pins.
- Regression: the same validator rejects malformed XML, missing `ProjectReference Include`, a forbidden Domain-to-Ecs edge and resulting cycle, a Content Pipeline 3.8.4 pin, and a MonoGame package placed in Domain.
- Existing assembly/API/component/version/schedule/typed-ID/headless checks remain active.

### Second-pass files

- `src/ShipGame.Ecs/EcsWorld.cs`
- `src/ShipGame.Telemetry/TelemetryContracts.cs`
- `tests/ShipGame.Ecs.Tests/UnitTest1.cs`
- `tests/ShipGame.Telemetry.Tests/UnitTest1.cs`
- `tests/ShipGame.Architecture.Tests/ProjectArchitecturePolicy.cs`
- `tests/ShipGame.Architecture.Tests/UnitTest1.cs`
- `docs/reports/P0_FOUNDATION/reviewer-report.md`

### Exact second-pass gate summary

- Initial full `dotnet test` exposed that .NET's `FrozenDictionary` itself implements a mutable dictionary interface even though mutation is unsupported: 59 passed, 1 failed. A private non-castable wrapper was added; no assertion or policy was weakened.
- Focused final suites: ECS 10/10, Telemetry 7/7, Architecture 8/8.
- Clean solution, restore, canonical script: exit 0; Content Builder 2/2; build 0 warnings/errors; 60 passed, 0 failed, 0 skipped.
- Explicit clean and incremental content builds: exit 0; each reported 2 succeeded, 0 failed.
- Headless smoke: exit 0; restore/content/build and deterministic Continue completed.
- DesktopVK bounded smoke: exit 0; Vulkan/Win32 surface, content, walking skeleton, RTX 3090, and swapchain recreation observed.
- Package resolution: exit 0; required runtime and builder packages resolve exactly to 3.8.5.
- Vulnerability audit: exit 0; runtime/test projects report no vulnerable packages. The previously documented official Content Pipeline transitive metadata still reports four high-advisory legacy packages.
- `dotnet list ShipGame.sln reference`: exit 1 because this CLI does not accept the solution for the reference verb. The corrected per-production-project loop exited 0 and listed all project edges; the XML architecture gate independently validates those exact edges.
- No-legacy scan: exit 0, no matches.
- Diff checks and IDE diagnostics: no whitespace or linter errors.

Full exact commands and observed results are appended to `commands-and-results.txt`.

### Remaining or disputed recheck findings

None. The three open major findings M1, M4, and M8 were reproduced and corrected. Independent acceptance recheck remains required.
