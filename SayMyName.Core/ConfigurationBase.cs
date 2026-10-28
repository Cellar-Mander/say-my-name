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
    public const int MaxCustomWordsLength = 2000;

    // Starts empty, as does each TriggerSettings.Channels: Json.NET adds to an existing set on load,
    // so pre-filled channels would resurrect ones the user had turned off. Normalize() fills in the
    // defaults instead. (ConfigurationRoundTripTests catches this.)
    public Dictionary<TriggerKind, TriggerSettings> Triggers { get; set; } = [];

    /// <summary>Match your name only as written (e.g. Rose but not "a rose").</summary>
    public bool NameCaseSensitive { get; set; } = true;

    /// <summary>Words and phrases for the Custom trigger, one per line.</summary>
    // A string rather than a list: Json.NET would append a loaded list to a pre-filled one
    public string CustomWords { get; set; } = "";

    /// <summary>Match custom words only as written.</summary>
    public bool CustomWordsCaseSensitive { get; set; }

    public bool PlaySound { get; set; } = true;

    /// <summary>The chat sound effect, as in &lt;se.1&gt; to &lt;se.16&gt;.</summary>
    public int SoundEffect { get; set; } = MinSoundEffect;

    /// <summary>Minimum time between alert sounds.</summary>
    public int CooldownSeconds { get; set; } = 5;

    /// <summary>Settings window sections shown expanded; any other, including ones added later, starts closed.</summary>
    // Null until loaded, for the same reason as Triggers: a pre-filled set would bring back sections
    // the user had closed. Normalize() opens only the trigger section for a config that never saved one.
    public HashSet<SettingsSection>? ExpandedSections { get; set; }

    /// <summary>Fills in defaults for unsaved triggers and pulls hand-edited values back into range.</summary>
    public void Normalize()
    {
        foreach (var kind in Core.Triggers.All)
        {
            Core.Triggers.Resolve(Triggers, kind);
        }

        SoundEffect = Math.Clamp(SoundEffect, MinSoundEffect, MaxSoundEffect);
        CooldownSeconds = Math.Clamp(CooldownSeconds, 0, MaxCooldownSeconds);
        CustomWords ??= "";
        if (CustomWords.Length > MaxCustomWordsLength)
        {
            CustomWords = CustomWords[..MaxCustomWordsLength];
        }
        ExpandedSections ??= [SettingsSection.Triggers];
    }
}
