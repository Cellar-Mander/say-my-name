namespace SayMyName.Core;

/// <summary>
/// Finds a character's first or last name, or one of a list of custom words and phrases, in chat
/// text as a whole word. Apostrophes and hyphens between letters join a word (Y'shtola, Mary-Jane), except a trailing
/// possessive 's, so "Rin's" mentions Rin but "Rin'ka" and "Mary-Rin" do not.
/// </summary>
public sealed class NameMatcher
{
    private const char CurlyApostrophe = '’';

    private readonly string[] parts;
    private readonly StringComparison comparison;

    /// <summary>The name or phrase list this matcher was built from.</summary>
    public string Source { get; }
    public bool CaseSensitive { get; }

    /// <param name="fullName">The character name, e.g. "Alice Smith". Empty matches nothing.</param>
    /// <param name="caseSensitive">
    /// Match the name only as written (title case), so a name like Rose doesn't fire on "a rose".
    /// </param>
    public NameMatcher(string fullName, bool caseSensitive)
        : this(fullName, fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), caseSensitive)
    {
    }

    private NameMatcher(string source, IEnumerable<string> parts, bool caseSensitive)
    {
        Source = source;
        CaseSensitive = caseSensitive;
        this.parts = [.. parts.Select(p => p.Replace(CurlyApostrophe, '\''))];
        comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    }

    /// <summary>
    /// Matches any of the words or phrases, one per line, each as a whole: "good morning" matches
    /// "Good morning!" but not "good" alone. Runs of spaces inside a phrase count as one; blank lines are skipped.
    /// </summary>
    public static NameMatcher ForPhrases(string lines, bool caseSensitive) =>
        new(lines,
            lines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
                .Where(phrase => phrase.Length > 0)
                .Distinct(),
            caseSensitive);

    public bool IsMatch(ReadOnlySpan<char> text)
    {
        // Chat text may use the typographic apostrophe where the name has a straight one, or the
        // reverse; normalising both sides keeps every index the same, so the word checks still hold
        if (!text.Contains(CurlyApostrophe))
        {
            return Find(text);
        }

        Span<char> straight = text.Length <= 512 ? stackalloc char[text.Length] : new char[text.Length];
        text.Replace(straight, CurlyApostrophe, '\'');
        return Find(straight);
    }

    private bool Find(ReadOnlySpan<char> text)
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
        return text[i] == '\''
            && i + 1 < text.Length && (text[i + 1] is 's' or 'S')
            && (i + 2 == text.Length || !IsWordChar(text[i + 2]));
    }

    private static bool IsJoinedOn(ReadOnlySpan<char> text, int i) =>
        IsJoiner(text[i]) && i + 1 < text.Length && IsWordChar(text[i + 1]);

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

    // Curly apostrophes are already straightened by IsMatch
    private static bool IsJoiner(char c) => c is '\'' or '-';
}
