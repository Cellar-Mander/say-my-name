using System.Text;

namespace SayMyName.Core;

/// <summary>The nicknames and custom words lists, saved as one entry per line, as the settings window edits them.</summary>
public static class EntryList
{
    /// <summary>The entries of a saved list: each line trimmed, blank lines dropped.</summary>
    public static List<string> Split(string lines) =>
        [.. lines.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0)];

    public static string Join(IEnumerable<string> entries) => string.Join('\n', entries);

    /// <summary>
    /// How many bytes of <paramref name="utf8"/> hold its first <paramref name="maxChars"/> characters (UTF-16
    /// code units, as string.Length counts them), never splitting a character in two.
    /// </summary>
    public static int Utf8PrefixLength(ReadOnlySpan<byte> utf8, int maxChars)
    {
        var bytes = 0;
        var chars = 0;
        while (bytes < utf8.Length)
        {
            Rune.DecodeFromUtf8(utf8[bytes..], out var rune, out var consumed);
            chars += rune.Utf16SequenceLength;
            if (chars > maxChars)
            {
                break;
            }
            bytes += consumed;
        }
        return bytes;
    }
}
