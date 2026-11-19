using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace JobGaugeEvolved.Native;

/// <summary>
/// Hides the game's own job gauge addons while a replacement is active, and shows them again afterwards.
/// </summary>
/// <remarks>
/// The addon keeps running (so the game keeps updating it and its HUD Layout position stays readable); only its
/// root node is hidden, right before every draw. Only addons hidden here are ever shown again here.
/// </remarks>
internal sealed unsafe class NativeGaugeHider : IDisposable
{
    private readonly string[] addons;
    private readonly HashSet<string> wanted = new();
    private readonly HashSet<string> hiddenByUs = new();

    public NativeGaugeHider(IEnumerable<string> addonNames)
    {
        addons = addonNames.Distinct().ToArray();
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, addons, OnPreDraw);
    }

    public void Dispose()
    {
        Service.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, addons, OnPreDraw);
        foreach (var name in hiddenByUs.ToArray()) {
            Restore(name);
        }
    }

    /// <summary>Whether an addon should be hidden. Call on the framework thread.</summary>
    public void SetHidden(string addon, bool hidden)
    {
        if (hidden) {
            wanted.Add(addon);
        }
        else if (wanted.Remove(addon) && hiddenByUs.Contains(addon)) {
            Restore(addon);
        }
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args)
    {
        try {
            if (!wanted.Contains(args.AddonName)) {
                return;
            }

            var unit = (AtkUnitBase*)args.Addon.Address;
            if (unit == null || unit->RootNode == null) {
                return;
            }

            unit->RootNode->ToggleVisibility(false);
            hiddenByUs.Add(args.AddonName);
        }
        catch (Exception e) {
            Service.Log.Error(e, "Could not hide the game's gauge");
        }
    }

    private void Restore(string name)
    {
        hiddenByUs.Remove(name);
        try {
            var unit = (AtkUnitBase*)Service.GameGui.GetAddonByName(name).Address;
            if (unit != null && unit->RootNode != null) {
                unit->RootNode->ToggleVisibility(true);
            }
        }
        catch (Exception e) {
            Service.Log.Error(e, "Could not show the game's gauge again");
        }
    }
}
