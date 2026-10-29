using Newtonsoft.Json;

namespace SayMyName.Core;

/// <summary>
/// Saves a trigger's channels by name, so channels can be removed or reordered without
/// changing what a saved config means. Unknown names (e.g. a channel since removed) and anything
/// that isn't a name are dropped.
/// </summary>
public sealed class ChannelSetConverter : JsonConverter<HashSet<ChatChannel>>
{
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
            if (reader.TokenType == JsonToken.String && ByName.TryGetValue((string)reader.Value!, out var channel))
            {
                channels.Add(channel);
            }
            reader.Skip();
        }
        return channels;
    }
}
