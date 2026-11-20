# Job Gauge Evolved

A Dalamud plugin for FINAL FANTASY XIV. Build and test commands are in `README.md`.

For Dalamud API knowledge, toolchain setup, gotchas and the release process, use the `dalamud-plugin` skill in
[EvolvedXIV/dalamud_reference](https://github.com/EvolvedXIV/dalamud_reference): attach that repository to the
session (it loads the skill automatically), or clone it and read `.claude/skills/dalamud-plugin/SKILL.md`.
Record what you learn here in that skill, not in this repository.

`scripts/make_repo_json.py` and `.github/workflows/` have template copies in that skill (`templates/`, with the
name replaced by `MyPlugin`). Keep both in step when changing either.

Releases update this plugin's entry in `repo.json` in [EvolvedXIV/releases](https://github.com/EvolvedXIV/releases).
The Release workflow does that with the `RELEASES_TOKEN` secret; without it, run the workflow with *Update repo.json*
off and update `repo.json` through a PR on that repository (see the skill's `reference/release.md`).

Gauge art: edit the SVG in `art/`, then rebuild with `scripts/build_art.py` (see README) and commit the PNGs and
`layout.json`. Layer and anchor names used in code are listed in `Gauges/<Job>/<Job>Layers.cs`; the tests check that
each exists. Glows and highlight colours are not art layers: `GaugeCanvas.Glow`/`Tint` draw them from a layer's
outline (traced from its alpha by `ShapeLibrary`), additively via `AdditiveBlend` when the glow style asks. Glows are
drawn before the art: each pulse starts inside the outline (`Shape.Insets`), hidden by the opaque art.

The overlay is drawn above the game's UI; `Native/WindowOcclusion` finds the game windows above the job's own gauge
every frame, and `GaugeManager` draws the gauge once per uncovered piece (`Rendering/RectCut`), clipped to it.

A new job gauge implements `Gauges/IJobGauge.cs`, gets a config field in `PluginConfiguration` and is added to the
list in `GaugeManager`.
