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

    /// <summary>
    /// Whether the sender text names <paramref name="characterName"/>, ignoring the game's marks and any
    /// tags other plugins put around the name (Party Icons' job prefix, Player Tags' tags), so long as no
    /// letter touches the name.
    /// </summary>
    public static bool Is(ReadOnlySpan<char> senderText, string characterName)
    {
        if (characterName.Length == 0)
        {
            return false;
        }

        for (var from = 0; from <= senderText.Length - characterName.Length;)
        {
            var at = senderText[from..].IndexOf(characterName, StringComparison.Ordinal);
            if (at < 0)
            {
                return false;
            }
            at += from;
            var end = at + characterName.Length;
            if ((at == 0 || !char.IsLetter(senderText[at - 1])) && (end == senderText.Length || !char.IsLetter(senderText[end])))
            {
                return true;
            }
            from = at + 1;
        }
        return false;
    }
}
