# P0_FOUNDATION Independent Reviewer Report

## Identity

- Package: `P0_FOUNDATION`
- Branch: `p0-foundation`
- Pinned base: `a443f89c920e37b2d46385a1d18b2e34efe4892f`
- Candidate implementation: `38f2db9b429533653883a3d031299359b5d9fe7e`
- Evidence commit: `2bce17f86019b3da471c9259af44db3638032b95`
- Review range: `a443f89c920e37b2d46385a1d18b2e34efe4892f..38f2db9b429533653883a3d031299359b5d9fe7e`
- Reviewer made no tracked edits.

## Gate results

- `scripts/test.ps1`: exit 0.
- Headless smoke: exit 0.
- DesktopVK `WindowSmoke`: exit 0.
- Clean and incremental content builds: exit 0.
- Package listing: exit 0.
- No-legacy-pipeline scan: exit 0.
- Automated tests: 26 passed, 0 failed, 0 skipped.
- Tracked working tree remained clean.

These command exits do not establish acceptance. The DesktopVK/continue contract failed because Continue did not restore deterministic identity. Persistence compatibility/recovery, content extension, and architecture enforcement contracts also failed adversarial checks.

## Findings

### C1 — CRITICAL: malformed save envelopes can crash load

`PersistenceContracts.cs` dereferences required envelope members before validating them. Parseable JSON containing `"Versions": null` throws `NullReferenceException` instead of returning `Corrupt` and attempting backup recovery. All required envelope members must be validated before dereference. Malformed DTOs must classify as `Corrupt`, with regression coverage for null and missing members in both primary and backup files.

### M1 — MAJOR: raw ECS store mutation bypasses world safety

`EcsWorld.cs` publicly exposes mutable `ComponentStore<T>` operations. A stale destroyed `EntityId` can be inserted directly into a store, bypassing world generation validation and structural buffering and creating orphan dense entries. Raw mutation must become internal or world-validated, read/query access must be separated from mutation, mutation phases must be enforced, and stale/reuse/query-mutation/randomized regressions must be added.

### M2 — MAJOR: persistence ignores durable compatibility dimensions

Compatibility checks only the save schema. A correctly checksummed envelope with `Content = 999` loads as `Supported`. Every durable version and the catalog fingerprint must be classified, including explicit missing-content and incompatible outcomes, with independent tests.

### M3 — MAJOR: Continue does not restore deterministic simulation identity

`ShipGameHost` updates `_profile` after Continue, but `_simulation`, `_driver`, and RNG streams remain constructed from the default seed. Continue must load before construction or reconstruct the deterministic runtime after loading. An end-to-end regression must assert saved profile seed, run index, and first RNG output. The existing smoke only compares the persisted profile and is misleading.

### M4 — MAJOR: telemetry failure isolation and no-PII are unenforced

A cyclic arbitrary `Dictionary` payload throws `JsonException` while the failure flag remains false. Arbitrary object payloads permit PII and raw text. Introduce a bounded typed or whitelisted payload, reject prohibited fields/types, contain serialization and sink construction/write/disposal failures, and test PII, cycles, unsupported types, size bounds, and sink failures.

### M5 — MAJOR: authoritative hashes omit queued future commands

Two states hash equally when one contains a future `Confirm` command, then later diverge. Include deterministically sorted pending commands in the authoritative hash, or enforce a no-pending checkpoint invariant. Define every hashed field and add divergence-sensitivity tests.

### M6 — MAJOR: Content Builder contradicts the data-only extension contract

The C# Content Builder hardcodes exactly two source files while the extension note says manifest additions are data-only. Build validated manifest entries or use reviewed folder/type wildcard rules so valid additions compile/copy without builder code edits. Preserve the actual MonoGame 3.8.5 code-centric builder and do not add legacy MGCB.

### M7 — MAJOR: content definitions do not enforce typed, nonempty IDs

`ContentDefinition` stores IDs and references as strings, and an empty definition ID validates. Validate IDs and references through `ContentId` and expose typed IDs in validated runtime content, with regression tests.

### M8 — MAJOR: architecture tests enforce only a fraction of documented gates

Add robust project/assembly dependency graph and cycle checks across every production assembly; forbidden-reference checks; seeded RNG checks; typed-ID checks; migration/version declaration and continuity checks; component restrictions; explicit schedule registration; and headless initialization checks where appropriate. Policy tests must demonstrate rejection of representative violations rather than merely asserting current constants.

### m1 — MINOR: command-buffer failure replays applied commands

After a command throws, already-applied commands remain in the buffer and replay on the next apply. Define deterministic failure semantics and test them.

### m2 — MINOR: malformed manifest assets can throw `NullReferenceException`

A parseable manifest with null or missing `assets` throws instead of producing `ContentValidationException`. Normalize malformed manifest members and add regression coverage.

## Command evidence

The standard restore/build/test/content/launch commands all exited 0 and the original suite reported 26 passing tests. Adversarial reproductions nevertheless demonstrated:

- parseable null save members crash rather than recover;
- direct stale `EntityId` store insertion succeeds;
- a checksummed noncurrent content version is accepted;
- Continue retains the default simulation/RNG identity;
- cyclic or arbitrary telemetry payloads escape the isolation/privacy contract;
- future queued commands are absent from state hashes;
- manifest additions require Content Builder source edits;
- empty/untyped content IDs pass;
- documented architecture rules lack executable enforcement.

## Downstream impact

- Save corruption or malformed input can crash boot/continue and prevent known-good backup recovery.
- ECS lifetime and iteration invariants can be bypassed by feature systems, causing orphan components and nondeterministic queries.
- Unsupported content/generation/RNG/replay/telemetry state may load under false compatibility.
- Continued runs can use the wrong seed and RNG sequence, invalidating replay and generation identity.
- Telemetry can block play or persist prohibited personal/raw data.
- Equal checkpoints can conceal different future behavior.
- P1 content additions are not actually data-only.
- Feature packages can introduce unstable IDs and architecture violations without gate failures.

## Verdict

`REMEDIATE`

The candidate is not accepted. All substantiated critical and major findings require correction and independent recheck.
