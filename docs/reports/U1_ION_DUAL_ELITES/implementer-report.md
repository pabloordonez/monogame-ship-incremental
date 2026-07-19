# U1_ION_DUAL_ELITES — Implementer Report

## Mapping

| Requirement | Change |
|-------------|--------|
| Dual Ion elites | `WorldRun.ElitesRequired`, orchestrator multi-spawn with offsets, combat `MaxEliteSpawns` |
| Two cores | `DataCoreDropRequested` per elite death; loot core limit |
| Rare weapons | `RareAdvancedThreatWeapons` + mount override on threat spawn; hostile beam/seeker fire path |
| Docs | game-design, systems, content-catalog |

## Tests

New: `IonVeilRequiresTwoElitesAndTwoDataCoresBeforeExtraction`, `EliteDataCoreLimitAllowsConfiguredSecondCore`. Updated 10k-seed helper for elite/core counts.
