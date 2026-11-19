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
    }
}
