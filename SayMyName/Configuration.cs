using Dalamud.Configuration;
using SayMyName.Core;
using System;

namespace SayMyName;

[Serializable]
public class Configuration : ConfigurationBase, IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
