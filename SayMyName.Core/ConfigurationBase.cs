namespace SayMyName.Core;

/// <summary>The settings window's collapsible sections. Saved by number, so only append.</summary>
public enum SettingsSection
{
    Triggers,
    Sound,
    Channels,
}

/// <summary>
/// The saved settings, kept free of Dalamud so a save/load round trip can be tested here.
/// The plugin's Configuration adds Dalamud's interface and Save().
/// </summary>
public abstract class ConfigurationBase
{
    public const int MinSoundEffect = 1;
    public const int MaxSoundEffect = 16;
    public const int MaxCooldownSeconds = 60;

    // Starts empty, as does each TriggerSettings.Channels: Json.NET adds to an existing set on load,
    // so pre-filled channels would resurrect ones the user had turned off. Normalize() fills in the
    // defaults instead. (ConfigurationRoundTripTests catches this.)
    public Dictionary<TriggerKind, TriggerSettings> Triggers { get; set; } = [];

    /// <summary>Match your name only as written (e.g. Rose but not "a rose").</summary>
    public bool NameCaseSensitive { get; set; } = true;

    public bool PlaySound { get; set; } = true;

    /// <summary>The chat sound effect, as in &lt;se.1&gt; to &lt;se.16&gt;.</summary>
    public int SoundEffect { get; set; } = MinSoundEffect;

    /// <summary>Minimum time between alert sounds.</summary>
    public int CooldownSeconds { get; set; } = 5;

    /// <summary>Settings window sections the user folded away. Listing the closed ones lets new sections start open.</summary>
    public HashSet<SettingsSection> CollapsedSections { get; set; } = [];

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
}
