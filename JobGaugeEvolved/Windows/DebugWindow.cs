using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using JobGaugeEvolved.Gauges;

namespace JobGaugeEvolved.Windows;

/// <summary>Live values, and the game's own idea of the combo, to check the gauge against (<c>/jge debug</c>).</summary>
internal sealed class DebugWindow : Window
{
    private readonly GaugeManager gauges;

    public DebugWindow(Plugin plugin) : base("Job Gauge Evolved debug###JobGaugeEvolvedDebug")
    {
        gauges = plugin.Gauges;
        Size = new Vector2(620, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        if (gauges.Current is not { } gauge) {
            ImGui.TextWrapped("No gauge for your current job (or it is turned off). Switch to a job with a gauge.");
            return;
        }

        ImGui.TextUnformatted($"{gauge.Name} gauge");
        ImGui.Separator();
        gauge.DrawDebug();

        ImGui.Spacing();
        ImGui.Separator();
        var w = gauges.Windows;
        ImGui.TextUnformatted($"Game windows over the gauge: drawn after {w.ReferenceAddon} " +
                              $"(layer {w.Reference.Layer}, position {w.Reference.Index})");
        foreach (var c in w.Covering) {
            ImGui.TextUnformatted($"  {c.Name}  layer {c.Layer}  ({c.Rect.Min.X:0},{c.Rect.Min.Y:0})–({c.Rect.Max.X:0},{c.Rect.Max.Y:0})");
        }
    }
}
