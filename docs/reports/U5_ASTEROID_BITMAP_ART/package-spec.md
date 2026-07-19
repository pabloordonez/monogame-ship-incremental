# U5_ASTEROID_BITMAP_ART — Package Spec

## Gates

1. Intact asteroids draw a single atlas bitmap per size×kind×health tier; no UI ore-chip overlays and no `asteroids/break` tint overlays.
2. Authored frames exist for Small/Medium/Large × Ordinary/Ferrite/Lumen × healthy/cracked/shattered (27 rock sprites) with ore veins baked into ferrite/lumen PNGs.
3. Misfiled asteroid slots (`large/ferrite`, `large/ordinary`, `large/lumen`, `medium/lumen`, `small/lumen`, `break`) are replaced with coherent rock art; `asteroids/break` removed from the pack catalog.
4. On break, presentation spawns atlas debris bitmaps (`asteroids/debris/*`) from the rock center (not colored `Fill` squares); gameplay `PickupBurst` loot unchanged.
5. `pickups/ferrite` and `pickups/lumen` are crisp pixel nuggets consistent with debris art.
6. Living docs updated; content pack + Content/Gameplay tests green.
