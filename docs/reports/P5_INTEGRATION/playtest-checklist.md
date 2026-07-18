# Playtest checklist (Round A ready)

## Build

- [ ] `scripts/build.ps1` succeeds (content 16/0)
- [ ] Launch without `--smoke` opens DesktopVK window with title **SHIP GAME**
- [ ] Local telemetry file may appear under `%LocalAppData%/ShipGame/telemetry.jsonl` when consent enabled

## Fresh profile loop

- [ ] Title → Enter → Lobby shows balances and ship sprite
- [ ] M Map / L Loadout / R Research screens render panels (not solid-only clears)
- [ ] Map → Enter launches Run with background + ship/asteroid sprites + HUD
- [ ] WASD move, mouse aim, LMB fire, RMB mine, Space mobility, E extract, 1/2/3 upgrades, Esc pause
- [ ] After extract or failure, Summary shows result → Enter returns Lobby
- [ ] Continue (Title+C or relaunch) restores Lobby with banked resources

## Accessibility / options smoke

- [ ] Game remains playable with effects conceptually off (no mandatory bloom/particles)
- [ ] Keyboard path completes lobby navigation without gamepad
- [ ] Gamepad sticks/triggers produce flight commands when connected

## Telemetry

- [ ] With consent on, JSONL events append without blocking play
- [ ] With consent off / sink failure, play continues
