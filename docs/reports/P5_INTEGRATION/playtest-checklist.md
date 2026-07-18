# Playtest checklist (Round A ready)

## Build

- [ ] `scripts/build.ps1` succeeds (content 16/0)
- [ ] Launch without `--smoke` opens DesktopVK window with title **SHIP GAME**
- [ ] Local telemetry file may appear under `%LocalAppData%/ShipGame/telemetry.jsonl` when consent enabled

## Fresh profile loop

- [ ] Title shows readable **SHIP GAME** text and focusable New Game / Continue / Quit buttons
- [ ] Lobby shows Ferrite/Lumen/Cores numbers and focusable Map / Loadout / Research / Settings buttons
- [ ] Mouse click or keyboard (arrows/Enter, hotkeys M/L/R/O) navigates lobby destinations
- [ ] Focused/hovered controls show a selection frame; pressed controls inset/brighten
- [ ] Map lists environments with selection chrome; Up/Down or click selects unlocked rows; Enter/click Launch
- [ ] Loadout / Research rows are focusable; Enter/click equips or purchases when ready
- [ ] Settings is reachable from Lobby/Pause; toggles Shake / Flashes / Vibration / Consent / Master volume
- [ ] Run HUD shows Hull/Shield bars + icons, Ferrite/objective counts, and control reminder
- [ ] WASD move shows thrust trail + move tick; background parallax scrolls; mouse aim reticle visible
- [ ] LMB fire / RMB mine show muzzle / mining ray; Space dash, E extract, Esc pause
- [ ] Upgrade offer lists named choices with icons; 1/2/3, Enter, or mouse click selects
- [ ] After extract or failure, Summary shows EXTRACTED/FAILED and banked amounts → Enter/click returns Lobby
- [ ] Continue (Title+C or relaunch) restores Lobby with banked resources

## Accessibility / options smoke

- [ ] Game remains playable with Flashes / Screen Shake toggled off in Settings
- [ ] Keyboard path completes lobby navigation without gamepad or mouse
- [ ] Mouse path completes Title → Lobby → Map → Launch without requiring hotkeys
- [ ] Gamepad sticks/triggers produce flight commands when connected

## Telemetry

- [ ] With consent on, JSONL events append without blocking play
- [ ] With consent off / sink failure, play continues
