# Job Gauge Evolved

A Dalamud plugin for FINAL FANTASY XIV. Build and test commands are in `README.md`.

For Dalamud API knowledge, toolchain setup, gotchas and the release process, use the `dalamud-plugin` skill in
[EvolvedXIV/dalamud_reference](https://github.com/EvolvedXIV/dalamud_reference): attach that repository to the
session (it loads the skill automatically), or clone it and read `.claude/skills/dalamud-plugin/SKILL.md`.
Record what you learn here in that skill, not in this repository.

`scripts/make_repo_json.py` and `.github/workflows/` have template copies in that skill (`templates/`, with the
name replaced by `MyPlugin`). Keep both in step when changing either.

Releases update this plugin's entry in `repo.json` in
[EvolvedXIV/releases](https://github.com/EvolvedXIV/releases); the workflow needs the `RELEASES_TOKEN` secret.

Gauge art: edit the SVG in `art/`, then re-render with `scripts/render_art.js` (see README) and commit the PNGs and
`layout.json`. Layer and anchor names used in code are listed in `Gauges/<Job>/<Job>Layers.cs`; the tests check that
each exists. A new job gauge implements `Gauges/IJobGauge.cs`, gets a config field in `PluginConfiguration` and is
added to the list in `GaugeManager`.
