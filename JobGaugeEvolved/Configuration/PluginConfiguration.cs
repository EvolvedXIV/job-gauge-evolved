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

    [Newtonsoft.Json.JsonIgnore]
    public IEnumerable<GaugeConfig> AllGauges
    {
        get { yield return Viper; }
    }

    public void Sanitize()
    {
        Viper ??= new ViperConfig();
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
