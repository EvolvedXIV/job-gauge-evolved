using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using JobGaugeEvolved.Configuration;
using JobGaugeEvolved.Gauges;
using JobGaugeEvolved.Windows;

namespace JobGaugeEvolved;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/jge";

    private readonly WindowSystem windowSystem = new("JobGaugeEvolved");
    private readonly ConfigWindow configWindow;
    private readonly DebugWindow debugWindow;

    internal PluginConfiguration Configuration { get; }
    internal GaugeManager Gauges { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Service>();

        Configuration = PluginConfiguration.Load();
        Gauges = new GaugeManager(Configuration);

        configWindow = new ConfigWindow(this);
        debugWindow = new DebugWindow(this);
        windowSystem.AddWindow(configWindow);
        windowSystem.AddWindow(debugWindow);

        // The gauges subscribed to Draw first, so these windows are drawn on top of them.
        pluginInterface.UiBuilder.Draw += windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        pluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;

        Service.CommandManager.AddHandler(Command, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Job Gauge Evolved settings. \"/jge debug\" shows live gauge values.",
        });
    }

    public void Dispose()
    {
        Service.CommandManager.RemoveHandler(Command);

        Service.PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        Service.PluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        Service.PluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        windowSystem.RemoveAllWindows();

        configWindow.SaveIfNeeded(force: true);
        Gauges.Dispose();
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant()) {
            case "":
            case "config":
                configWindow.Toggle();
                break;

            case "debug":
                debugWindow.Toggle();
                break;

            default:
                Service.ChatGui.Print($"Unknown argument. Usage: {Command} [config|debug]", "Job Gauge Evolved");
                break;
        }
    }
}
