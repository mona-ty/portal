using Dalamud.Configuration;
using Dalamud.Plugin;

namespace HelloDalamudSample;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool EnabledGreeting { get; set; } = true;

    public void Save(IDalamudPluginInterface pi)
    {
        pi.SavePluginConfig(this);
    }
}

