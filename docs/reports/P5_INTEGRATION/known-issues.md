# P5 known issues

| Severity | Issue | Notes |
|---|---|---|
| Major (filed) | Manual 1080p 10-minute frame capture not executed on reference machine | Trace: no `perf-1080p-*.csv` artifact produced. Owner: P5 integration. Capture on Win11 / RTX 3090 reference after warm-up; criteria in validation-and-backlog.md |
| Major (filed) | Full 50-run golden-path reliability marathon not executed | Trace: automated probe `ReliabilityProbe_TenHarnessExtracts_NoDuplicateRewardCorruption` covers 10/50 extracts with duplicate-commit guard; remaining 40 for Round A marathon |
| Minor | Procedural 5×7 pixel font (not a designed typeface) | Readable MVP labels; replace with authored bitmap font when art pass lands |
| Minor | Save catalog fingerprint remains `foundation-catalog-v1` for MetaSession migrate compatibility | Content fingerprint validated via `RuntimeContentCatalog` at load; unify in follow-up migration |
| Minor | `CompleteViaHarness` used by window-smoke/auto tests for terminal resolution | Human play uses live combat/mining/extract; harness is test-only, not a player debug command |
| Info | Candidate art retained under waivers | See `waivers.md` |
| Info | No music files (intentional) | Brief only |
