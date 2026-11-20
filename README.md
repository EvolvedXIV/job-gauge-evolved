# Job Gauge Evolved

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for FINAL FANTASY XIV that draws improved job gauges. Each
gauge can be resized (50% to 300%), moved anywhere or tied to the game gauge's HUD Layout position, recoloured,
and turned on or off per job.

## Gauges

### Viper

The Vipersight blades and the Serpent Offerings gauge in one twinblade of blackened, engraved Gothic ironwork: a
straight sword points out to each side, and their hilts meet at a glass orb in the middle, the Serpent Offerings
vessel, which fills with liquid from the bottom (50, enough to Reawaken, is the equator). An arched band over the
orb holds the Anguine Tribute stones and a band under it the Rattling Coil rubies, each set into the metal. The left blade stands for
the Steel Fangs button, the right blade for the Reaving Fangs button.

| Combo step | What lights up |
|---|---|
| 1. Steel Fangs or Reaving Fangs | The blade of the button your Honed buff empowers: Honed Steel → left, Honed Reavers → right. Nothing without a Honed buff. |
| 2. Hunter's Sting or Swiftskin's Sting | The blade that leads to the finisher your venom empowers: a flank venom → left (Hunter's Sting), a rear venom → right (Swiftskin's Sting). Both blades without a venom (the opener's first combo). |
| 3. Flank finisher (after Hunter's Sting) | A glow around the orb in the flank centre colour (green → yellow), plus the blade of the finisher your venom empowers, in the flank blade colour: Flankstung → left (Flanksting Strike), Flanksbane → right (Flanksbane Fang). The orb and both blades without a flank venom (the opener's first combo). |
| 3. Rear finisher (after Swiftskin's Sting) | A glow around the orb in the rear centre colour (red → yellow), plus the blade of the empowered finisher, in the rear blade colour: Hindstung → left (Hindsting Strike), Hindsbane → right (Hindsbane Fang). The orb and both blades without a rear venom. |

The blades follow your buffs as they come and go, like the game's Vipersight: when they run out, the blades go dark
without you casting anything. The game is read 20 times a second. `/jge debug` says whether the plugin and the
game's own gauge agree on which blades are lit.

A lit blade gets its colour added to the steel (like GIMP's Addition mode, 50% by default), so the texture shows
through, and glows. Glows sit around the shape, never over it, and pulse: each pulse comes out from behind the
shape, so it first shows as a highlight hugging the edge, then drifts out until there is a gap, fades and starts
again (every 2 seconds by default). Glows and colours are drawn by the plugin from each part's outline, traced from
the art's transparency.

The area-of-effect combo doesn't change the blades. The tribute stones light up only while Reawakened, with the time
left shown above them; the rubies glow for each Rattling Coil you have. The gauge value is shown on the orb.

Colours you can change: recommended blade, flank blade, flank centre, rear blade, rear centre, liquid, tributes
and coils. Each is a single colour or a two-colour gradient across the shape (top to bottom, or along the blades
from tip to hilt). Two glows can be set apart: *Blade and stone glow* (blades, tributes, coils) and *Centre glow*
(the orb at the finishers). Each has its pulse (on/off, seconds per pulse, the gap), width, brightness and additive
blending (adds the glow to what is behind it, like GIMP's Addition mode; turn it off to paint it over instead).

## Installing

1. In game, open `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Add this URL, tick *Enabled*, and save:

   ```
   https://raw.githubusercontent.com/EvolvedXIV/releases/main/repo.json
   ```

3. Open `/xlplugins`, search for **Job Gauge Evolved** and install it.

## Usage

| Command      | Effect                                                         |
|--------------|----------------------------------------------------------------|
| `/jge`       | Open the settings window                                       |
| `/jge debug` | Live gauge values next to the game's own view of the combo     |

In the settings window:

- **Unlock gauges** lets you drag the gauge on screen. It also shows out of combat. Closing the window locks it again.
- **Preview** shows a gauge with made-up values on any job, in or out of combat, either cycling through every combo
  state or showing the one you pick.
- Every job has its own section with an on/off box: size, position mode (free, or *Follow HUD Layout*: where the
  game's own gauge is, scaled like it, plus your offset), "only in combat", hiding behind game windows, hiding the
  game's own gauge, and the job's colours.
- **Windows that never cover gauges** (under *General*): addon names, comma separated, for a game window that hides
  the gauge when it shouldn't. `/jge debug` lists the windows covering the gauge right now.

Gauges are drawn by the plugin on top of the game's UI, but by default the parts under a game window (inventory,
character, retainers, tooltips, menus, …) are left out, so the gauge looks behind them like the game's own gauge.
The windows are checked every frame. Gauges are hidden in PvP, cutscenes and GPose, and when you hide the UI.

## Building

Requires the .NET 10 SDK and a Dalamud installation (XIVLauncher installs one; otherwise point `DALAMUD_HOME` at
an extracted [Dalamud distribution](https://goatcorp.github.io/dalamud-distrib/latest.zip)).

```
dotnet build JobGaugeEvolved.sln -c Release
dotnet test JobGaugeEvolved.Tests -c Release
```

The plugin ends up in `JobGaugeEvolved/bin/x64/Release/`. To load it in game, add that folder under
*Dalamud Settings → Experimental → Dev Plugin Locations*.

The tests cover the parts that do not need the game (the Viper combo rules, colours, the art layout, outlines and
glow pulses, window cut-outs, positioning and settings) and run on any platform.

### Gauge art

The art is original (drawn after the game's own gauges) and lives in `art/` as SVG, one group per layer. The plugin
only needs the layers' images: glows and highlight colours are not art, they are drawn from the outlines of the
`blade`, `vessel-ring`, `gem-socket` and `coil-socket` layers, traced from their transparency when the plugin starts. A layer
can come with a height map (`data-pass="height"`, white = high), which `scripts/build_art.py` turns into lit,
textured metal: relief, grime in the recesses, grain, scratches and highlights, with a light from straight above.
A `matte` pass dulls parts such as leather, a `glow` pass is laid on top unlit (eyes). Every layer is then written
as `JobGaugeEvolved/Assets/<Job>/<layer>@1x|2x|3x.png` with a `layout.json` of positions. The PNGs are committed and
embedded in the plugin, so building never needs the script. After changing an SVG, run:

```
python3 scripts/build_art.py art/viper/viper.svg JobGaugeEvolved/Assets/Viper
```

It needs Python with numpy and Pillow, and Node with Playwright and Chromium (`scripts/render_art.js` rasterises
the SVG). Layers marked `data-kind="mask"` are white or grey and take the colour settings at draw time; the others
are drawn as they are.

Notes on the Dalamud API, the toolchain and the release process shared by all EvolvedXIV plugins live in
[EvolvedXIV/dalamud_reference](https://github.com/EvolvedXIV/dalamud_reference).

## Releasing

1. Bump `<Version>` in `JobGaugeEvolved/JobGaugeEvolved.csproj` and push.
2. Then either:
   - run the **Release** workflow (GitHub → Actions → Release → Run workflow, on the default branch). It builds and
     tests the plugin, publishes a GitHub release `v<version>` here with `JobGaugeEvolved.zip`, and updates this
     plugin's entry in `repo.json` in [EvolvedXIV/releases](https://github.com/EvolvedXIV/releases) (untick
     *Update repo.json* to skip that); or
   - push a tag `v<version>` (e.g. `git tag v1.2.0.0 && git push origin v1.2.0.0`), from any branch. That publishes
     the release only and leaves `repo.json` alone.

Either way the run's summary shows this version's `repo.json` entry, ready to paste in by hand. The entry is
generated by `scripts/make_repo_json.py` from the plugin manifest; entries of other plugins are left alone.

Updating `repo.json` from the workflow needs a repository secret `RELEASES_TOKEN` holding a fine-grained personal
access token (or GitHub App token) with **Contents: Read and write** on `EvolvedXIV/releases`.

## Layout

| Path                             | What it does                                                         |
|----------------------------------|----------------------------------------------------------------------|
| `JobGaugeEvolved/Gauges/`        | Picking the gauge for your job, the overlay window and positioning   |
| `JobGaugeEvolved/Gauges/Viper/`  | The Viper gauge: game state, combo rules, drawing, settings          |
| `JobGaugeEvolved/Rendering/`     | Art layout, textures, glows, tinted drawing, window cut-outs, text   |
| `JobGaugeEvolved/Native/`        | Hiding the game's own gauges, finding the game windows that cover them |
| `JobGaugeEvolved/Windows/`       | Settings and debug windows                                           |
| `JobGaugeEvolved/Configuration/` | Saved settings                                                       |
| `art/`, `scripts/build_art.py`   | Gauge art sources and the scripts that render and light them         |
