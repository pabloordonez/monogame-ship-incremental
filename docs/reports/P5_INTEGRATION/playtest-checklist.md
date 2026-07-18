# Playtest checklist (Round A ready)

## Build

- [ ] `scripts/build.ps1` succeeds (content 16/0)
- [ ] Launch without `--smoke` opens DesktopVK window with title **SHIP GAME**
- [ ] Local telemetry file may appear under `%LocalAppData%/ShipGame/telemetry.jsonl` when consent enabled

## Fresh profile loop

- [ ] Title shows readable **SHIP GAME** text and focusable New Game / Continue / Quit buttons
- [ ] **STATION** shows banked Ferrite/Lumen/Cores and Map / Loadout / Research / Upgrades / Settings
- [ ] Mouse click or keyboard (arrows/Enter, hotkeys M/L/R/U/O) navigates station destinations
- [ ] Custom mouse cursor is visible over the virtual canvas (OS cursor hidden); brightens when pressed
- [ ] Focused/hovered controls show a selection frame; pressed controls inset/brighten
- [ ] Map lists environments with selection chrome; Up/Down or click selects unlocked rows; Enter/click Launch
- [ ] Loadout / Research / Upgrades rows are focusable; Research and Upgrades show costs and spend banked materials
- [ ] Settings is reachable from Station/Pause; toggles Shake / Flashes / Vibration / Consent / Master volume
- [ ] Run HUD shows Hull/Shield bars + icons, Ferrite/objective counts, and control reminder
- [ ] WASD move shows thrust trail + move tick; background parallax scrolls; mouse aim reticle visible
- [ ] LMB fire / RMB mine show muzzle / mining ray; Space dash, E extract, Esc pause
- [ ] **No mid-run upgrade modal** — flight continues until extract/fail
- [ ] Destroying enemies grants Ferrite salvage; mining asteroids grants Ferrite/Lumen; elite drops Data Core
- [ ] After extract or failure, Summary shows EXTRACTED/FAILED and banked amounts → Enter/click returns Station
- [ ] At Station, banked resources can buy Research and Upgrades; Continue restores Station with purchases

## Accessibility / options smoke

- [ ] Game remains playable with Flashes / Screen Shake toggled off in Settings
- [ ] Keyboard path completes station navigation without gamepad or mouse
- [ ] Mouse path completes Title → Station → Map → Launch without requiring hotkeys
- [ ] Gamepad sticks/triggers produce flight commands when connected

## Telemetry

- [ ] With consent on, JSONL events append without blocking play
- [ ] With consent off / sink failure, play continues
