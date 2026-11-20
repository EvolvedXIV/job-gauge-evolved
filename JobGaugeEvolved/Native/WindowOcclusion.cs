using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using JobGaugeEvolved.Rendering;

namespace JobGaugeEvolved.Native;

/// <summary>
/// Finds the game windows that the game would draw over a job's own gauge, so the replacement can leave those areas
/// out and look like it is behind them, as the original is. The rules are in <see cref="CoverRules"/>; this class only
/// reads the game's addons from the unit manager's depth layers.
/// </summary>
internal sealed unsafe class WindowOcclusion
{
    private CoveringWindow[] covering = [];
    private IReadOnlyList<string>? ownSource;
    private HashSet<string> own = new(StringComparer.Ordinal);
    private (GameUnit Unit, CoverVerdict Verdict)[] framed = [];

    /// <summary>The windows covering the gauge, as of the last <see cref="Update"/>.</summary>
    public IReadOnlyList<CoveringWindow> Covering => covering;

    /// <summary>Every visible window with a frame and what the rules made of it (for the debug window).</summary>
    public IReadOnlyList<(GameUnit Unit, CoverVerdict Verdict)> FramedWindows => framed;

    /// <summary>Where the job's own gauge addon was found (depth layer, position in it), or null.</summary>
    public (int Layer, int Index)? Gauge { get; private set; }

    public string GaugeAddon { get; private set; } = string.Empty;

    /// <summary>Looks at the game's windows again. Call on the framework thread, every frame.</summary>
    /// <param name="gaugeAddons">The job's own gauge addons; the first one gives the gauge's depth layer.</param>
    /// <param name="screen">The screen size in pixels, or zero if not known yet.</param>
    public void Update(IReadOnlyList<string> gaugeAddons, IReadOnlySet<string> ignored, Vector2 screen)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null || gaugeAddons.Count == 0) {
            covering = [];
            framed = [];
            return;
        }

        GaugeAddon = gaugeAddons[0];
        (int Layer, int Index)? gauge = null;
        var units = new List<GameUnit>();
        var layers = manager->AtkUnitManager.DepthLayers;
        for (var layer = 0; layer < layers.Length; layer++) {
            var list = layers[layer];
            var count = Math.Min(list.Count, list.Entries.Length);
            for (var i = 0; i < count; i++) {
                var unit = list.Entries[i].Value;
                if (unit == null) {
                    continue;
                }

                var name = unit->NameString;
                if (name == GaugeAddon) {
                    gauge = (layer, i);
                }

                var root = unit->RootNode;
                var visible = unit->IsVisible && unit->Alpha > 0 && root != null && root->IsVisible();

                // Position and root size times scale: the same numbers the HUD Layout placement uses.
                var rect = default(ScreenRect);
                if (root != null) {
                    var min = new Vector2(unit->X, unit->Y);
                    rect = new ScreenRect(min, min + new Vector2(root->Width, root->Height) * unit->Scale);
                }

                units.Add(new GameUnit(name, layer, unit->WindowNode != null, visible, rect));
            }
        }

        Gauge = gauge;
        if (!ReferenceEquals(ownSource, gaugeAddons)) {
            ownSource = gaugeAddons;
            own = gaugeAddons.ToHashSet(StringComparer.Ordinal);
        }

        covering = CoverRules.Select(units, gauge?.Layer, own, ignored, screen);
        framed = units.Where(u => u.Framed && u.Visible)
                      .Select(u => (u, CoverRules.Judge(u, gauge?.Layer, own, ignored, screen)))
                      .ToArray();
    }
}
