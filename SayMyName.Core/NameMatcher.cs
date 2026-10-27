namespace SayMyName.Core;

/// <summary>
/// Finds a character's first or last name in chat text as a whole word.
/// Apostrophes and hyphens between letters join a word (Y'shtola, Mary-Jane), except a trailing
/// possessive 's, so "Rin's" mentions Rin but "Rin'ka" and "Mary-Rin" do not.
/// </summary>
public sealed class NameMatcher
{
    private readonly string[] parts;
    private readonly StringComparison comparison;

    public string FullName { get; }
    public bool CaseSensitive { get; }

    /// <param name="fullName">The character name, e.g. "Alice Smith". Empty matches nothing.</param>
    /// <param name="caseSensitive">
    /// Match the name only as written (title case), so a name like Rose doesn't fire on "a rose".
    /// </param>
    public NameMatcher(string fullName, bool caseSensitive)
    {
        FullName = fullName;
        CaseSensitive = caseSensitive;
        parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    public bool IsMatch(ReadOnlySpan<char> text)
    {
        foreach (var part in parts)
        {
            var offset = 0;
            while (offset < text.Length)
            {
                var found = text[offset..].IndexOf(part, comparison);
                if (found < 0)
                {
                    break;
                }

                var start = offset + found;
                if (StartsWord(text, start) && EndsWord(text, start + part.Length))
                {
                    return true;
                }
                offset = start + 1;
            }
        }
        return false;
    }

    private static bool StartsWord(ReadOnlySpan<char> text, int i) =>
        i == 0 || !(IsWordChar(text[i - 1]) || (IsJoiner(text[i - 1]) && i >= 2 && IsWordChar(text[i - 2])));

    private static bool EndsWord(ReadOnlySpan<char> text, int i)
    {
        if (i == text.Length || (!IsWordChar(text[i]) && !IsJoinedOn(text, i)))
        {
            return true;
        }

        // Possessive: Alice's, ALICE'S
        return IsApostrophe(text[i])
            && i + 1 < text.Length && (text[i + 1] is 's' or 'S')
            && (i + 2 == text.Length || !IsWordChar(text[i + 2]));
    }

    private static bool IsJoinedOn(ReadOnlySpan<char> text, int i) =>
        IsJoiner(text[i]) && i + 1 < text.Length && IsWordChar(text[i + 1]);

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

    private static bool IsApostrophe(char c) => c is '\'' or '’';

    private static bool IsJoiner(char c) => IsApostrophe(c) || c == '-';
}
