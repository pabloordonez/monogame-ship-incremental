# P1_CONTENT_ART Candidate / Placeholder Waivers

Integration owner: `P1_CONTENT_ART` package specification (`docs/reports/P1_CONTENT_ART/package-spec.md`).

All retained candidates/placeholders are original repository-authored assets (authored pixel sprites packed into atlases, or synthesized PCM). License is `CC0-1.0` unless noted. No unknown or incompatible licenses. No music files.

## Residual human review (automated gates cover what they can)

Automated Content tests now enforce: opaque palette size ≤32, region silhouette/alpha occupancy, and grayscale-luminance (or silhouette-mask) distinctness for ore variants and friendly vs hostile projectiles. Residual human review still required before `approved` status:

- Native 640×360 effects-off playfeel (motion blur/particles disabled in a future render path).
- Full contact-sheet grayscale telegraphs under actual gameplay lighting, not only static atlas math.
- Multi-frame animation strips (metadata currently honest single-frame placeholders with reserved fps).

| Asset ID | Status | Reason retained | Replacement criterion | License / provenance |
|---|---|---|---|---|
| `data/title-placeholder` | placeholder | P0 walking-skeleton title text | Replace when title presentation is approved | CC0-1.0; original foundation text; owner `P0_FOUNDATION` |
| `atlases/player-modules` | candidate | Authored Wayfarer/modules sheet for pipeline + playfeel review | Approve after native-resolution silhouette review; preserve region IDs, frame tiers, pivots, hardpoints | CC0-1.0; authored sprites under `textures/sprites/` packed by ContentBuilder |
| `atlases/enemies-telegraphs` | candidate | Authored threat/telegraph sheet pending readability review | Native-resolution threat-readability review; preserve IDs/timing semantics | CC0-1.0; authored sprites packed by ContentBuilder |
| `atlases/asteroids-resources` | candidate | Authored mining/resource sheet pending review | Mining/resource review; preserve IDs/collision refs | CC0-1.0; authored sprites packed by ContentBuilder |
| `atlases/ui-icons` | candidate | Authored 32px UI glyph sheet pending comprehension testing | 24/32 UI comprehension testing; preserve icon region IDs | CC0-1.0; authored sprites packed by ContentBuilder |
| `art/contact-sheet` | candidate | Palette/silhouette contact sheet assembled from packed atlases | Archive/replace when visual candidates are approved | CC0-1.0; composited from packed atlas samples |
| `backgrounds/cinder-belt` | candidate | 640×360 environment plane for contrast tests | Effects-off contrast review; preserve ID + canvas | CC0-1.0; deterministic star field |
| `backgrounds/ion-veil` | candidate | 640×360 access-gate environment plane | Grayscale contrast review; preserve ID + canvas | CC0-1.0; deterministic star field |
| `audio/essential-cues` | placeholder | Essential original SFX bank for pipeline + cue IDs | Individually mixed original cues after event binding/loudness review | CC0-1.0; synthesized PCM; no samples |

Approved metadata entries (`data/atlas-*`, `data/sfx-cues`) are package-authored JSON and do not require candidate waivers.
