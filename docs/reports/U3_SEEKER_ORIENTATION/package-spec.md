# U3_SEEKER_ORIENTATION — Package Spec

## Goal

Seeker missiles render aligned with their velocity heading (not perpendicular). Simulation `Transform2.Rotation` tracks velocity while homing/flying.

## Owned paths

- `src/ShipGame.Gameplay/Contexts/FlightCombatContext.cs` (`GuideProjectile`)
- `src/ShipGame.Gameplay/Systems/IntegrateFlightMovementSystem.cs`
- `src/ShipGame.Game/Meta/Screens/RunMetaScreen.cs` (seeker draw offset)
- `tests/ShipGame.Gameplay.Tests/FlightCombatTests.cs`
- This report folder

## Gates

1. Homing updates keep `Transform2.Rotation` on velocity angle.
2. Straight missiles keep rotation on velocity heading each integrate tick.
3. Presentation cancels ship-centric `+π/2` for +X-facing `projectiles/seeker` art.
4. Regression test: seeker rotation tracks position delta heading while turning.
5. Existing seeker fire/home tests still pass.

## Exclusions

Asteroids, elites, extraction UI, enemy weapons.
