using System;
using System.Collections.Generic;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using JobGaugeEvolved.Rendering;

namespace JobGaugeEvolved.Native;

/// <summary>A game window that covers a gauge, for drawing around it and for the debug window.</summary>
internal readonly record struct CoveringWindow(string Name, int Layer, ScreenRect Rect);

/// <summary>
/// Finds the game windows that the game would draw over a job's own gauge, so the replacement can leave those areas
/// out and look like it is behind them, as the original is.
/// </summary>
/// <remarks>
/// The game draws its windows depth layer by depth layer (13 lists in the unit manager), each list in order. A window
/// covers the gauge if it is in a higher layer than the job's own gauge addon, or later in the same layer with a real
/// window frame (so HUD elements beside the gauge don't count). The job's own addon stays loaded while hidden, so its
/// place is known even with "hide the game's gauge" on.
/// </remarks>
internal sealed unsafe class WindowOcclusion
{
    // Used until the job's gauge addon has been seen once. (unverified: checked in game with the debug window)
    private const int DefaultLayer = 4;

    private (int Layer, int Index) reference = (DefaultLayer, int.MaxValue);
    private CoveringWindow[] covering = [];

    /// <summary>The windows covering the gauge, as of the last <see cref="Update"/>.</summary>
    public IReadOnlyList<CoveringWindow> Covering => covering;

    /// <summary>Where the job's own gauge addon is in the game's draw order (layer, position in that layer).</summary>
    public (int Layer, int Index) Reference => reference;

    public string ReferenceAddon { get; private set; } = string.Empty;

    /// <summary>Looks at the game's windows again. Call on the framework thread, every frame.</summary>
    public void Update(string referenceAddon, IReadOnlySet<string> ignored)
    {
        var manager = RaptureAtkUnitManager.Instance();
        if (manager == null) {
            covering = [];
            return;
        }

        var layers = manager->AtkUnitManager.DepthLayers;

        // Where the job's own gauge is drawn.
        if (referenceAddon != ReferenceAddon) {
            ReferenceAddon = referenceAddon;
            reference = (DefaultLayer, int.MaxValue);
        }

        for (var layer = 0; layer < layers.Length; layer++) {
            var list = layers[layer];
            for (var i = 0; i < list.Count; i++) {
                var unit = list.Entries[i].Value;
                if (unit != null && unit->NameString == referenceAddon) {
                    reference = (layer, i);
                }
            }
        }

        var found = new List<CoveringWindow>();
        for (var layer = reference.Layer; layer < layers.Length; layer++) {
            var list = layers[layer];
            for (var i = 0; i < list.Count; i++) {
                var sameLayer = layer == reference.Layer;
                if (sameLayer && i <= reference.Index) {
                    continue;
                }

                var unit = list.Entries[i].Value;
                if (unit == null || !unit->IsVisible || unit->Alpha == 0 || unit->RootNode == null ||
                    !unit->RootNode->IsVisible()) {
                    continue;
                }

                // In the gauge's own layer, only real windows count, not HUD elements next to the gauge.
                if (sameLayer && unit->WindowNode == null) {
                    continue;
                }

                var name = unit->NameString;
                if (ignored.Contains(name)) {
                    continue;
                }

                FFXIVClientStructs.FFXIV.Common.Math.Bounds bounds;
                if (unit->WindowNode != null) {
                    unit->GetWindowBounds(&bounds);
                }
                else {
                    unit->GetRootBounds(&bounds);
                }

                var rect = new ScreenRect(new Vector2(bounds.Pos1.X, bounds.Pos1.Y), new Vector2(bounds.Pos2.X, bounds.Pos2.Y));
                if (!rect.IsEmpty) {
                    found.Add(new CoveringWindow(name, layer, rect));
                }
            }
        }

        covering = found.ToArray();
    }
}
