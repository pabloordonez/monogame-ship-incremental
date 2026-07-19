# U5_ASTEROID_BITMAP_ART — Implementer Report

## Summary

Replaced asteroid ore chips, `asteroids/break` tint overlays, and colored Fill break particles with authored pixel-art bitmaps: 27 size×kind×health-tier rock frames (ore veins baked into ferrite/lumen), 6 debris chunks for core explosions, and crisp ferrite/lumen pickups. Misfiled large/medium asteroid slots and the square break overlay were removed/replaced. Atlases repacked at 1024² to fit the expanded region set.

## Gates

1. Single bitmap draw per rock; no UI chips / no `asteroids/break` overlay — **PASS** (`RunMetaScreen`).
2. 27 rock sprites + baked ore — **PASS** (`content/source/textures/sprites/asteroids/**`).
3. Misfiled slots fixed; `asteroids/break` removed from catalog — **PASS**.
4. Break VFX uses `asteroids/debris/*` via particle `RegionId` — **PASS** (`ParticlePresets.AsteroidBreak`).
5. Crisp `pickups/ferrite` / `pickups/lumen` — **PASS**; run view draws pickup atlas regions.
6. Docs + tests — **PASS** (commands below).

## Key paths

- `tools/pixel_clean_asteroids.py` — AI candidate → exact-frame pixel-clean + damage tiers
- `content/source/textures/sprites/asteroids/` — authored frames
- `src/ShipGame.Gameplay/World/AsteroidSizing.cs` — health-tier region IDs
- `src/ShipGame.Game/Meta/Screens/RunMetaScreen.cs` — single-region draw
- `src/ShipGame.Game/Presentation/Particles/*` + `MvpPresentation.DrawParticles` — sprite debris

## Risks

- AI downsample + procedural reinforcement may still need art polish after playfeel review.
- Global atlas size bumped to 1024 (all atlases); within 2048 MVP max.
