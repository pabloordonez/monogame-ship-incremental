# P5 known issues

| Severity | Issue | Notes |
|---|---|---|
| Major (filed) | Manual 1080p 10-minute frame capture not executed on reference machine | Automated suite + window smoke green; file for Round A hardware capture |
| Major (filed) | 50-run golden-path reliability marathon not executed | Headless `SmokeRunner` covers fresh+continue twice; marathon deferred to playtest prep |
| Minor | UI labels are bar proxies (no bitmap font atlas yet) | Screens remain distinct via panels/sprites/HUD; replace with font atlas post-MVP if needed |
| Minor | Save catalog fingerprint remains `foundation-catalog-v1` for MetaSession migrate compatibility | Content fingerprint validated via `RuntimeContentCatalog` at load; unify in follow-up migration |
| Minor | `CompleteViaHarness` used by window-smoke/auto tests for terminal resolution | Human play uses live combat/mining/extract; harness is test-only, not a player debug command |
| Info | Candidate art retained under waivers | See `waivers.md` |
| Info | No music files (intentional) | Brief only |
