namespace SayMyName.Core;

public static class ChatSender
{
    /// <summary>
    /// The character name in a chat line's sender text, without the marks the game adds around it
    /// (party slot numbers, friend and cross-world icons, which are symbols or private-use glyphs).
    /// </summary>
    public static ReadOnlySpan<char> Name(ReadOnlySpan<char> senderText)
    {
        var start = 0;
        while (start < senderText.Length && !char.IsLetter(senderText[start]))
        {
            start++;
        }

        var end = senderText.Length;
        while (end > start && !char.IsLetter(senderText[end - 1]))
        {
            end--;
        }

        return senderText[start..end];
    }

    /// <summary>Whether the sender text names <paramref name="characterName"/>, ignoring the game's marks.</summary>
    public static bool Is(ReadOnlySpan<char> senderText, string characterName) =>
        characterName.Length > 0 && Name(senderText).SequenceEqual(characterName);
}
