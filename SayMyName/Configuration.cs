using Dalamud.Configuration;
using SayMyName.Core;
using System;

namespace SayMyName;

[Serializable]
public class Configuration : ConfigurationBase, IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Dev plugin only: set while the self-test has changed the settings in memory, so they aren't saved
    internal bool SavesBlocked;

    public void Save()
    {
        if (!SavesBlocked)
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
