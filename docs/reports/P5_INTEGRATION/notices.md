# Notices / packaging notes

- Platform: Windows DesktopVK (MonoGame 3.8.5)
- Build: `scripts/build.ps1` then run `src/ShipGame.Game` Release output
- Content: C# ContentBuilder → `content/generated/DesktopVK/Content` (not MGCB)
- Saves: `%LocalAppData%/ShipGame/profile-v2.json` (migrates foundation `profile.json`)
- Telemetry: local JSONL only; no external transmission
- Credits: asset attribution in `content/source/data/asset-manifest.json` + P1/P5 waivers
- Music: absent by design; SFX cue data may load without blocking
