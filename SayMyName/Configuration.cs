using Dalamud.Configuration;
using SayMyName.Core;
using System;
using System.Collections.Generic;

namespace SayMyName;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    // Starts empty: Json.NET adds to an existing collection on load, so pre-filled defaults would
    // resurrect channels the user had turned off. Normalize() fills in the defaults instead.
    public Dictionary<TriggerKind, TriggerSettings> Triggers { get; set; } = [];

    /// <summary>Fills in defaults for unsaved triggers and drops channels they may not use.</summary>
    public void Normalize()
    {
        foreach (var kind in Core.Triggers.All)
        {
            Core.Triggers.Resolve(Triggers, kind);
        }
    }

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
