using Newtonsoft.Json;

namespace SayMyName.Core;

/// <summary>
/// Player chat channels a trigger can listen on. Tells are deliberately absent.
/// Saved by name (see <see cref="ChannelSetConverter"/>), so entries may be removed or reordered, but not renamed.
/// </summary>
public enum ChatChannel
{
    Say,
    CustomEmote,
    Yell,
    Shout,
    /// <summary>Your party, on your world or cross-world.</summary>
    Party,
    Alliance,
    FreeCompany,
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
}

public static class Triggers
{
    public static readonly IReadOnlyList<TriggerKind> All = Enum.GetValues<TriggerKind>();

    // Standard emote lines are written by the game, never by a player, so only the Emote trigger listens there
    private static readonly ChatChannel[] TextChannels = [.. Enum.GetValues<ChatChannel>().Where(c => c != ChatChannel.StandardEmote)];
    // Party and alliance members are often nearby, so the sender can be checked for targeting you
    private static readonly ChatChannel[] YouChannels = [ChatChannel.Say, ChatChannel.CustomEmote, ChatChannel.Party, ChatChannel.Alliance];
    // Large groups talk all day, mostly not to you, so these start unticked
    private static readonly HashSet<ChatChannel> OffByDefault =
    [
        ChatChannel.Alliance, ChatChannel.FreeCompany, ChatChannel.NoviceNetwork,
        .. Enum.GetValues<ChatChannel>().Where(c => c is >= ChatChannel.Linkshell1 and <= ChatChannel.CrossLinkshell8),
    ];

    /// <summary>
    /// Triggers whose channels are chosen in settings; the Emote trigger only ever listens on standard
    /// emotes, and Target Speaks on every channel a player writes in.
    /// </summary>
    public static readonly IReadOnlyList<TriggerKind> WithChannelChoice = [.. All.Where(k => k is not (TriggerKind.Emote or TriggerKind.TargetSpeaks))];

    /// <summary>Channels a player can choose, those usable by the most triggers first; ties keep declaration order.</summary>
    public static readonly IReadOnlyList<ChatChannel> ChannelsByUsage =
        [.. TextChannels.OrderByDescending(c => WithChannelChoice.Count(k => AllowedChannels(k).Contains(c)))];

    /// <summary>Channels the trigger may be set to listen on; none for a trigger without a channel choice.</summary>
    public static IReadOnlyList<ChatChannel> AllowedChannels(TriggerKind kind) => kind switch
    {
        TriggerKind.Name or TriggerKind.Custom => TextChannels,
        TriggerKind.You => YouChannels,
        _ => [],
    };

    /// <summary>
    /// Whether the trigger is on and listens on <paramref name="channel"/>. The Emote trigger always listens on
    /// standard emotes and Target Speaks on every channel a player writes in; the others on their chosen channels.
    /// </summary>
    public static bool Listens(TriggerKind kind, TriggerSettings settings, ChatChannel channel) => settings.Enabled && kind switch
    {
        TriggerKind.Emote => channel == ChatChannel.StandardEmote,
        TriggerKind.TargetSpeaks => channel != ChatChannel.StandardEmote,
        _ => settings.Channels.Contains(channel),
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
        Channels = [.. AllowedChannels(kind).Where(c => !OffByDefault.Contains(c))],
    };
}

public class TriggerSettings
{
    public bool Enabled { get; set; }
    [JsonConverter(typeof(ChannelSetConverter))]
    public HashSet<ChatChannel> Channels { get; set; } = [];
}
