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
| 1. Steel Fangs or Reaving Fangs | The blade of the button your Honed buff empowers: Honed Steel → left, Honed Reavers → right. Both when you have neither (opener). |
| 2. Hunter's Sting or Swiftskin's Sting | The blade that leads to the finisher your venom empowers: a flank venom → left (Hunter's Sting), a rear venom → right (Swiftskin's Sting). Both when you have none. |
| 3. Flank finisher (after Hunter's Sting) | Both blades in the flank colour (green → yellow). The blade of the finisher your venom empowers is brighter: Flankstung → left (Flanksting Strike), Flanksbane → right (Flanksbane Fang). |
| 3. Rear finisher (after Swiftskin's Sting) | The orb in the rear colour (red → yellow), plus the blade of the empowered finisher: Hindstung → left (Hindsting Strike), Hindsbane → right (Hindsbane Fang). |

The area-of-effect combo doesn't change the blades. The tribute stones light up only while Reawakened, with the time
left shown above them; the rubies glow for each Rattling Coil you have. The gauge value is shown on the orb.

Colours you can change: recommended blade, flank, rear, liquid, tributes and coils. Each is a single colour or a
two-colour gradient across the shape (top to bottom or left to right).

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
  game's own gauge is, scaled like it, plus your offset), "only in combat", hiding the game's own gauge, and the
  job's colours.

Gauges are drawn by the plugin on top of the game's UI. They are hidden in PvP, cutscenes and GPose, and when you
hide the UI.

## Building

Requires the .NET 10 SDK and a Dalamud installation (XIVLauncher installs one; otherwise point `DALAMUD_HOME` at
an extracted [Dalamud distribution](https://goatcorp.github.io/dalamud-distrib/latest.zip)).

```
dotnet build JobGaugeEvolved.sln -c Release
dotnet test JobGaugeEvolved.Tests -c Release
```

The plugin ends up in `JobGaugeEvolved/bin/x64/Release/`. To load it in game, add that folder under
*Dalamud Settings → Experimental → Dev Plugin Locations*.

The tests cover the parts that do not need the game (the Viper combo rules, colours, the art layout, positioning
and settings) and run on any platform.

### Gauge art

The art is original and lives in `art/` as SVG, one group per layer. `scripts/render_art.js` renders every layer
to `JobGaugeEvolved/Assets/<Job>/<layer>@1x|2x|3x.png` and writes `layout.json` with each layer's position. The
PNGs are committed and embedded in the plugin, so building never needs the script. After changing an SVG, run:

```
node scripts/render_art.js art/viper/viper.svg JobGaugeEvolved/Assets/Viper
```

It needs Node and Playwright with Chromium. Layers marked `data-kind="mask"` are white or grey and take the colour
settings at draw time; the others are drawn as they are.

Notes on the Dalamud API, the toolchain and the release process shared by all EvolvedXIV plugins live in
[EvolvedXIV/dalamud_reference](https://github.com/EvolvedXIV/dalamud_reference).

## Releasing

1. Bump `<Version>` in `JobGaugeEvolved/JobGaugeEvolved.csproj` and push to the default branch.
2. Run the **Release** workflow (GitHub → Actions → Release → Run workflow, on the default branch).

The workflow builds and tests the plugin, publishes a GitHub release `v<version>` here with `JobGaugeEvolved.zip`,
and then updates this plugin's entry in `repo.json` in [EvolvedXIV/releases](https://github.com/EvolvedXIV/releases).

One-time setup: the workflow needs a repository secret `RELEASES_TOKEN` holding a fine-grained personal access
token (or GitHub App token) with **Contents: Read and write** on `EvolvedXIV/releases`.

## Layout

| Path                             | What it does                                                         |
|----------------------------------|----------------------------------------------------------------------|
| `JobGaugeEvolved/Gauges/`        | Picking the gauge for your job, the overlay window and positioning   |
| `JobGaugeEvolved/Gauges/Viper/`  | The Viper gauge: game state, combo rules, drawing, settings          |
| `JobGaugeEvolved/Rendering/`     | Art layout, textures, tinted drawing and gauge text                  |
| `JobGaugeEvolved/Native/`        | Hiding the game's own gauges                                         |
| `JobGaugeEvolved/Windows/`       | Settings and debug windows                                           |
| `JobGaugeEvolved/Configuration/` | Saved settings                                                       |
| `art/`, `scripts/render_art.js`  | Gauge art sources and the script that renders them                   |
