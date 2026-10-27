namespace SayMyName.Core;

/// <summary>Player chat channels a trigger can listen on. Tells are deliberately absent.</summary>
public enum ChatChannel
{
    Say,
    Yell,
    Shout,
    Party,
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
    /// <summary>An emote at you, or a 'you' message while targeting you.</summary>
    Targeted,
    /// <summary>Your current target speaks.</summary>
    TargetSpeaks,
}

public static class Triggers
{
    public static readonly IReadOnlyList<TriggerKind> All = Enum.GetValues<TriggerKind>();

    private static readonly ChatChannel[] AllChannels = Enum.GetValues<ChatChannel>();
    private static readonly ChatChannel[] SayAndEmotes = [ChatChannel.Say, ChatChannel.StandardEmote, ChatChannel.CustomEmote];

    /// <summary>Every channel, those usable by the most triggers first; ties keep declaration order.</summary>
    public static readonly IReadOnlyList<ChatChannel> ChannelsByUsage =
        [.. AllChannels.OrderByDescending(c => All.Count(k => AllowedChannels(k).Contains(c)))];

    /// <summary>Channels the trigger may be configured to listen on.</summary>
    public static IReadOnlyList<ChatChannel> AllowedChannels(TriggerKind kind) => kind switch
    {
        TriggerKind.Targeted or TriggerKind.TargetSpeaks => SayAndEmotes,
        _ => AllChannels,
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

        var allowed = AllowedChannels(kind);
        settings.Channels.RemoveWhere(c => !allowed.Contains(c));
        return settings;
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
