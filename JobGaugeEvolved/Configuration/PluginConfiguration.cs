using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace JobGaugeEvolved.Configuration;

[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // One field per job gauge.
    public ViperConfig Viper = new();

    /// <summary>Game windows (addon names, comma separated) that never cover a gauge, for overlays that are see-through.</summary>
    public string IgnoredWindows = string.Empty;

    [Newtonsoft.Json.JsonIgnore]
    public IReadOnlySet<string> IgnoredWindowNames { get; private set; } = new HashSet<string>();

    [Newtonsoft.Json.JsonIgnore]
    public IEnumerable<GaugeConfig> AllGauges
    {
        get { yield return Viper; }
    }

    public void Sanitize()
    {
        Viper ??= new ViperConfig();
        IgnoredWindows ??= string.Empty;
        IgnoredWindowNames = NameList.Parse(IgnoredWindows);
        foreach (var gauge in AllGauges) {
            gauge.Sanitize();
        }
    }

    public static PluginConfiguration Load()
    {
        PluginConfiguration? config = null;
        try {
            config = Service.PluginInterface.GetPluginConfig() as PluginConfiguration;
        }
        catch (Exception e) {
            Service.Log.Error(e, "Could not read the configuration, starting from defaults");
        }

        config ??= new PluginConfiguration();
        config.Sanitize();
        return config;
    }

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
