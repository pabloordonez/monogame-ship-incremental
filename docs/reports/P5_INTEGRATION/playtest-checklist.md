# Playtest checklist (Round A ready)

## Build

- [ ] `scripts/build.ps1` succeeds (content 16/0)
- [ ] Launch without `--smoke` opens DesktopVK window with title **SHIP GAME**
- [ ] Local telemetry file may appear under `%LocalAppData%/ShipGame/telemetry.jsonl` when consent enabled

## Fresh profile loop

- [ ] Title shows readable **SHIP GAME** text and Enter/C/Esc hints
- [ ] Lobby shows Ferrite/Lumen/Cores numbers and control list (M/L/R/Enter)
- [ ] Map lists environments in text; Enter launches when unlocked
- [ ] Run HUD shows Hull/Shield/Ferrite/objective counts and control reminder
- [ ] WASD move, mouse aim, LMB fire, RMB mine, Space dash, E extract, 1/2/3 upgrades, Esc pause
- [ ] After extract or failure, Summary shows EXTRACTED/FAILED and banked amounts → Enter returns Lobby
- [ ] Continue (Title+C or relaunch) restores Lobby with banked resources

## Accessibility / options smoke

- [ ] Game remains playable with effects conceptually off (no mandatory bloom/particles)
- [ ] Keyboard path completes lobby navigation without gamepad
- [ ] Gamepad sticks/triggers produce flight commands when connected

## Telemetry

- [ ] With consent on, JSONL events append without blocking play
- [ ] With consent off / sink failure, play continues
