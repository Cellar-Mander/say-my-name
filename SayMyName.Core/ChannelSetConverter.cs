using Newtonsoft.Json;

namespace SayMyName.Core;

/// <summary>
/// Saves a trigger's channels by name, so channels can be removed or reordered without
/// changing what a saved config means. Loads names, and the numbers older versions saved.
/// Unknown names and numbers (e.g. a channel since removed) are dropped.
/// </summary>
public sealed class ChannelSetConverter : JsonConverter<HashSet<ChatChannel>>
{
    // What each number meant when channels were saved by number; never change it.
    // Cross-world party (4) now counts as Party, and PvP teams (7) are gone from the game.
    private static readonly ChatChannel?[] LegacyNumbers =
    [
        ChatChannel.Say, ChatChannel.Yell, ChatChannel.Shout, ChatChannel.Party, null,
        ChatChannel.Alliance, ChatChannel.FreeCompany, null, ChatChannel.NoviceNetwork,
        ChatChannel.Linkshell1, ChatChannel.Linkshell2, ChatChannel.Linkshell3, ChatChannel.Linkshell4,
        ChatChannel.Linkshell5, ChatChannel.Linkshell6, ChatChannel.Linkshell7, ChatChannel.Linkshell8,
        ChatChannel.CrossLinkshell1, ChatChannel.CrossLinkshell2, ChatChannel.CrossLinkshell3, ChatChannel.CrossLinkshell4,
        ChatChannel.CrossLinkshell5, ChatChannel.CrossLinkshell6, ChatChannel.CrossLinkshell7, ChatChannel.CrossLinkshell8,
        ChatChannel.StandardEmote, ChatChannel.CustomEmote,
    ];

    // Exact names only: Enum.TryParse would also accept "5" or "say"
    private static readonly Dictionary<string, ChatChannel> ByName =
        Enum.GetValues<ChatChannel>().ToDictionary(c => c.ToString());

    public override void WriteJson(JsonWriter writer, HashSet<ChatChannel>? value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var channel in value ?? [])
        {
            writer.WriteValue(channel.ToString());
        }
        writer.WriteEndArray();
    }

    public override HashSet<ChatChannel> ReadJson(JsonReader reader, Type objectType, HashSet<ChatChannel>? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        HashSet<ChatChannel> channels = [];
        if (reader.TokenType != JsonToken.StartArray)
        {
            reader.Skip();
            return channels;
        }

        while (reader.Read() && reader.TokenType != JsonToken.EndArray)
        {
            ChatChannel? channel = reader.TokenType switch
            {
                JsonToken.String when ByName.TryGetValue((string)reader.Value!, out var c) => c,
                JsonToken.Integer when reader.Value is long n && n >= 0 && n < LegacyNumbers.Length => LegacyNumbers[n],
                _ => null,
            };
            reader.Skip();
            if (channel is { } known)
            {
                channels.Add(known);
            }
        }
        return channels;
    }
}
