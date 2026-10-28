namespace SayMyName.Core;

/// <summary>Player chat channels a trigger can listen on. Tells are deliberately absent.</summary>
public enum ChatChannel
{
    Say,
    Yell,
    Shout,
    /// <summary>Your party, on your world or cross-world.</summary>
    Party,
    /// <summary>Saved by older versions; cross-world party lines now count as <see cref="Party"/>. Not choosable.</summary>
    // Kept so later channels keep their saved numbers
    CrossParty,
    Alliance,
    FreeCompany,
    PvPTeam,
    NoviceNetwork,
    Linkshell1,
    Linkshell2,
    Linkshell3,
    Linkshell4,
    Linkshell5,
    Linkshell6,
    Linkshell7,
    Linkshell8,
    CrossLinkshell1,
    CrossLinkshell2,
    CrossLinkshell3,
    CrossLinkshell4,
    CrossLinkshell5,
    CrossLinkshell6,
    CrossLinkshell7,
    CrossLinkshell8,
    StandardEmote,
    CustomEmote,
}

public enum TriggerKind
{
    /// <summary>Another player says your name.</summary>
    Name,
    /// <summary>Someone says one of your custom words or phrases.</summary>
    Custom,
    /// <summary>Another player uses a standard emote on you; /em custom emotes are text, not emotes.</summary>
    Emote,
    /// <summary>A player targeting you says a 'you' word.</summary>
    You,
    /// <summary>Your current target speaks.</summary>
    TargetSpeaks,
    /// <summary>Saved by older versions; split into <see cref="Emote"/> and <see cref="You"/> on load. Not in <see cref="Triggers.All"/>.</summary>
    // Kept so a config that still names it loads: Json.NET rejects an unknown dictionary key
    Targeted,
}

public static class Triggers
{
    public static readonly IReadOnlyList<TriggerKind> All = [.. Enum.GetValues<TriggerKind>().Where(k => k != TriggerKind.Targeted)];

    // Standard emote lines are written by the game, never by a player, so only the Emote trigger listens there
    private static readonly ChatChannel[] TextChannels = [.. Enum.GetValues<ChatChannel>().Where(c => c is not (ChatChannel.StandardEmote or ChatChannel.CrossParty))];
    private static readonly ChatChannel[] StandardEmoteOnly = [ChatChannel.StandardEmote];
    private static readonly ChatChannel[] SayAndCustomEmotes = [ChatChannel.Say, ChatChannel.CustomEmote];
    // Party and alliance members are often nearby, so the sender can be checked for targeting you
    private static readonly ChatChannel[] YouChannels = [ChatChannel.Say, ChatChannel.Party, ChatChannel.Alliance, ChatChannel.CustomEmote];

    /// <summary>Triggers whose channels are chosen in settings; the Emote trigger only ever listens on standard emotes.</summary>
    public static readonly IReadOnlyList<TriggerKind> WithChannelChoice = [.. All.Where(k => k != TriggerKind.Emote)];

    /// <summary>Channels a player can choose, those usable by the most triggers first; ties keep declaration order.</summary>
    public static readonly IReadOnlyList<ChatChannel> ChannelsByUsage =
        [.. TextChannels.OrderByDescending(c => WithChannelChoice.Count(k => AllowedChannels(k).Contains(c)))];

    /// <summary>Channels the trigger may listen on.</summary>
    public static IReadOnlyList<ChatChannel> AllowedChannels(TriggerKind kind) => kind switch
    {
        TriggerKind.Emote => StandardEmoteOnly,
        TriggerKind.You => YouChannels,
        TriggerKind.TargetSpeaks => SayAndCustomEmotes,
        _ => TextChannels,
    };

    /// <summary>
    /// The saved settings for <paramref name="kind"/>, adding defaults if none are saved
    /// and dropping channels the trigger may not use (e.g. from a hand-edited config).
    /// </summary>
    public static TriggerSettings Resolve(IDictionary<TriggerKind, TriggerSettings> saved, TriggerKind kind)
    {
        if (!saved.TryGetValue(kind, out var settings))
        {
            settings = Default(kind);
            saved[kind] = settings;
        }

        // Its channel can no longer be unticked, so an older config that unticked it turns the trigger off instead
        if (kind == TriggerKind.Emote && settings.Channels.Add(ChatChannel.StandardEmote))
        {
            settings.Enabled = false;
        }

        var allowed = AllowedChannels(kind);
        settings.Channels.RemoveWhere(c => !allowed.Contains(c));
        return settings;
    }

    /// <summary>
    /// Replaces an older version's Targeted settings with Emote and You settings that keep its
    /// on/off and channels; Resolve() then drops the channels each may not use.
    /// </summary>
    public static void Migrate(IDictionary<TriggerKind, TriggerSettings> saved)
    {
        if (saved.Remove(TriggerKind.Targeted, out var targeted))
        {
            saved.TryAdd(TriggerKind.Emote, new() { Enabled = targeted.Enabled, Channels = [.. targeted.Channels] });
            saved.TryAdd(TriggerKind.You, new() { Enabled = targeted.Enabled, Channels = [.. targeted.Channels] });
        }
    }

    public static TriggerSettings Default(TriggerKind kind) => new()
    {
        // Watching the target is opt-in; the others are what people install the plugin for
        Enabled = kind != TriggerKind.TargetSpeaks,
        Channels = [.. AllowedChannels(kind)],
    };
}

public class TriggerSettings
{
    public bool Enabled { get; set; }
    public HashSet<ChatChannel> Channels { get; set; } = [];
}
