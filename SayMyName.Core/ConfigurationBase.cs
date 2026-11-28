namespace SayMyName.Core;

/// <summary>The settings window's collapsible sections. Saved by number, so only append.</summary>
public enum SettingsSection
{
    Triggers,
    Sound,
    Channels,
    Highlight,
}

/// <summary>One character's lists, each saved as one entry per line (see <see cref="EntryList"/>).</summary>
/// <param name="Nicknames">Nicknames for the Name trigger, matched like the character's name.</param>
/// <param name="CustomWords">Words and phrases for the Custom trigger.</param>
// Strings rather than lists: Json.NET would append a loaded list to a pre-filled one
public sealed record CharacterLists(string Nicknames, string CustomWords)
{
    public static readonly CharacterLists Empty = new("", "");
}

/// <summary>
/// The saved settings, kept free of Dalamud so a save/load round trip can be tested here.
/// The plugin's Configuration adds Dalamud's interface and Save().
/// </summary>
public abstract class ConfigurationBase
{
    public const int MinSoundEffect = 1;
    public const int MaxSoundEffect = 16;
    /// <summary>Quietest alert volume, in percent; 0 would only repeat Play a sound off.</summary>
    public const int MinSoundVolume = 5;
    public const int MaxSoundVolume = 100;
    public const int MaxCooldownSeconds = 60;
    /// <summary>How many entries the settings window lets the nicknames list, or the custom words list, hold.</summary>
    public const int MaxListEntries = 12;
    /// <summary>The longest nickname or custom word, in characters, the settings window takes.</summary>
    public const int MaxEntryLength = 64;
    /// <summary>UIColor sheet row meaning no colour change.</summary>
    public const ushort NoColor = 0;

    // Starts empty, as does each TriggerSettings.Channels: Json.NET adds to an existing set on load,
    // so pre-filled channels would resurrect ones the user had turned off. Normalize() fills in the
    // defaults instead. (ConfigurationRoundTripTests catches this.)
    public Dictionary<TriggerKind, TriggerSettings> Triggers { get; set; } = [];

    /// <summary>Match your name only as written (e.g. Rose but not "a rose").</summary>
    public bool NameCaseSensitive { get; set; } = true;

    /// <summary>
    /// Each character's nicknames and custom words, by content ID. A character with neither has no entry.
    /// Releases before these were per character kept one pair of lists for every character; a config
    /// saved by one still holds them, under names nothing reads, so they are dropped on the next save.
    /// </summary>
    // Starts empty, as Json.NET adds loaded entries to an existing dictionary
    public Dictionary<ulong, CharacterLists> Characters { get; set; } = [];

    /// <summary>The lists of the character with this content ID; empty for one with none saved, or 0 (none logged in).</summary>
    // No entry is ever stored under 0: SetListsFor ignores it and Normalize() drops a hand-edited one
    public CharacterLists ListsFor(ulong contentId) => Characters.GetValueOrDefault(contentId, CharacterLists.Empty);

    /// <summary>Stores a character's lists, dropping its entry once both are empty. Ignores content ID 0.</summary>
    public void SetListsFor(ulong contentId, CharacterLists lists)
    {
        if (contentId == 0)
        {
            return;
        }
        if (lists.Nicknames.Length == 0 && lists.CustomWords.Length == 0)
        {
            Characters.Remove(contentId);
        }
        else
        {
            Characters[contentId] = lists;
        }
    }

    /// <summary>Match custom words only as written.</summary>
    public bool CustomWordsCaseSensitive { get; set; }

    /// <summary>Target Speaks alerts only for lines your target sends while targeting you, rather than every line they send.</summary>
    public bool TargetSpeaksOnlyTargetingYou { get; set; } = true;

    public bool PlaySound { get; set; } = true;

    /// <summary>The chat sound effect, as in &lt;se.1&gt; to &lt;se.16&gt;.</summary>
    public int SoundEffect { get; set; } = 16;

    /// <summary>The alert sound's volume in percent, within the game's own volume settings. 100 plays it as the game does.</summary>
    public int SoundVolume { get; set; } = MaxSoundVolume;

    /// <summary>Minimum time between alert sounds.</summary>
    public int CooldownSeconds { get; set; } = 5;

    /// <summary>Colour the words that set off a trigger (your name, custom words, 'you' words) in chat, whether or not a sound plays.</summary>
    public bool Highlight { get; set; } = true;

    /// <summary>UIColor sheet row for highlighted text, or <see cref="NoColor"/>.</summary>
    public ushort HighlightTextColor { get; set; } = 535;

    /// <summary>UIColor sheet row for the highlighted text's edge glow, or <see cref="NoColor"/>.</summary>
    public ushort HighlightGlowColor { get; set; } = 18;

    /// <summary>Colour the names of inactive chat tabs that show a line which set off an alert, until the tab is opened.</summary>
    public bool HighlightTabs { get; set; } = true;

    /// <summary>UIColor sheet row for a highlighted tab's name; never <see cref="NoColor"/>, which would be HighlightTabs off.</summary>
    public ushort TabHighlightColor { get; set; } = DefaultTabHighlightColor;

    private const ushort DefaultTabHighlightColor = 576;

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
        SoundVolume = Math.Clamp(SoundVolume, MinSoundVolume, MaxSoundVolume);
        CooldownSeconds = Math.Clamp(CooldownSeconds, 0, MaxCooldownSeconds);
        // Lists are not cut to MaxListEntries or MaxEntryLength: one over a cap (hand-edited) keeps its entries
        Characters ??= [];
        Characters.Remove(0);
        foreach (var (id, lists) in Characters.ToList())
        {
            SetListsFor(id, new CharacterLists(lists?.Nicknames ?? "", lists?.CustomWords ?? ""));
        }
        ExpandedSections ??= [SettingsSection.Triggers];
        // Older versions offered None for the tab colour, which meant no tab highlighting
        if (TabHighlightColor == NoColor)
        {
            HighlightTabs = false;
            TabHighlightColor = DefaultTabHighlightColor;
        }
    }
}
