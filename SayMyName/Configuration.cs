using Dalamud.Configuration;
using SayMyName.Core;
using System;
using System.Collections.Generic;

namespace SayMyName;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public const int MinSoundEffect = 1;
    public const int MaxSoundEffect = 16;
    public const int MaxCooldownSeconds = 60;

    public int Version { get; set; } = 0;

    // Starts empty: Json.NET adds to an existing collection on load, so pre-filled defaults would
    // resurrect channels the user had turned off. Normalize() fills in the defaults instead.
    public Dictionary<TriggerKind, TriggerSettings> Triggers { get; set; } = [];

    /// <summary>Match your name only as written (e.g. Rose but not "a rose").</summary>
    public bool NameCaseSensitive { get; set; } = true;

    public bool PlaySound { get; set; } = true;

    /// <summary>The chat sound effect, as in &lt;se.1&gt; to &lt;se.16&gt;.</summary>
    public int SoundEffect { get; set; } = MinSoundEffect;

    /// <summary>Minimum time between alert sounds.</summary>
    public int CooldownSeconds { get; set; } = 5;

    /// <summary>Fills in defaults for unsaved triggers and pulls hand-edited values back into range.</summary>
    public void Normalize()
    {
        foreach (var kind in Core.Triggers.All)
        {
            Core.Triggers.Resolve(Triggers, kind);
        }

        SoundEffect = Math.Clamp(SoundEffect, MinSoundEffect, MaxSoundEffect);
        CooldownSeconds = Math.Clamp(CooldownSeconds, 0, MaxCooldownSeconds);
    }

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
