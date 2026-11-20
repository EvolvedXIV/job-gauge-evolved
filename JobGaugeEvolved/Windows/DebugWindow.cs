using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using JobGaugeEvolved.Gauges;
using JobGaugeEvolved.Native;
using JobGaugeEvolved.Rendering;

namespace JobGaugeEvolved.Windows;

/// <summary>Live values, and the game's own idea of the combo, to check the gauge against (<c>/jge debug</c>).</summary>
internal sealed class DebugWindow : Window
{
    private readonly GaugeManager gauges;

    public DebugWindow(Plugin plugin) : base("Job Gauge Evolved debug###JobGaugeEvolvedDebug")
    {
        gauges = plugin.Gauges;
        Size = new Vector2(720, 420);
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
        ImGui.TextUnformatted($"Game windows (hide behind them: {(gauge.Config.HideBehindWindows ? "on" : "off")})");
        ImGui.TextUnformatted(w.Gauge is { } at
            ? $"  The game's {w.GaugeAddon}: depth layer {at.Layer}, position {at.Index}"
            : $"  The game's {w.GaugeAddon}: not found among the game's windows");
        ImGui.TextUnformatted(gauges.LastDrawn is { } drawn ? $"  This gauge on screen: {Show(drawn)}" : "  This gauge is not shown");

        ImGui.TextUnformatted("  Windows with a frame:");
        if (w.FramedWindows.Count == 0) {
            ImGui.TextDisabled("    none visible");
        }

        foreach (var (unit, verdict) in w.FramedWindows) {
            var overlap = gauges.LastDrawn is { } g && unit.Rect.Overlaps(g) ? ", over the gauge" : string.Empty;
            ImGui.TextUnformatted($"    {unit.Name}  layer {unit.Layer}  {Show(unit.Rect)}  {verdict}{overlap}");
        }

        ImGui.TextUnformatted("  Covering the gauge without a frame (above its layer):");
        foreach (var c in w.Covering) {
            if (!IsFramed(w, c.Name)) {
                ImGui.TextUnformatted($"    {c.Name}  layer {c.Layer}  {Show(c.Rect)}");
            }
        }

        static string Show(ScreenRect r) => $"({r.Min.X:0},{r.Min.Y:0})–({r.Max.X:0},{r.Max.Y:0})";

        static bool IsFramed(WindowOcclusion w, string name)
        {
            foreach (var (unit, _) in w.FramedWindows) {
                if (unit.Name == name) {
                    return true;
                }
            }

            return false;
        }
    }
}
