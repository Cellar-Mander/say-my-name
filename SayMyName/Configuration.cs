using Dalamud.Configuration;
using SayMyName.Core;
using System;

namespace SayMyName;

[Serializable]
public class Configuration : ConfigurationBase, IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
