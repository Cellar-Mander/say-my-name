namespace SayMyName.Core;

/// <summary>
/// Finds a character's first or last name or a nickname, or one of a list of custom words and phrases, in chat
/// text as a whole word. Apostrophes and hyphens between letters join a word (Y'shtola, Mary-Jane), except a trailing
/// possessive 's, so "Rin's" mentions Rin but "Rin'ka" and "Mary-Rin" do not.
/// Names and nicknames also match stretched (Riiin, Rinnn) or spelled out (R-I-N, r i n); custom phrases match only as written.
/// A run of whitespace in chat text matches the single space between the words of a nickname or phrase.
/// </summary>
public sealed class NameMatcher
{
    private const char CurlyApostrophe = '’';

    private readonly string[] parts;
    private readonly StringComparison comparison;
    // Per part, its letter runs for stretched matching; null when the matcher only matches as written
    private readonly (int Start, int Length)[][]? runs;

    /// <summary>The name or phrase list this matcher was built from.</summary>
    public string Source { get; }
    /// <summary>The nickname list this matcher was built from; empty for a phrase matcher.</summary>
    public string Nicknames { get; }
    public bool CaseSensitive { get; }

    /// <param name="fullName">The character name, e.g. "Alice Smith". Empty matches nothing.</param>
    /// <param name="caseSensitive">
    /// Match the name only as written (title case), so a name like Rose doesn't fire on "a rose".
    /// </param>
    /// <param name="nicknames">
    /// Nicknames, one per line, each matched as a whole like a name part: "Big Al" matches "Big Aaal's"
    /// but not "Big" alone. Blank lines are skipped.
    /// </param>
    public NameMatcher(string fullName, bool caseSensitive, string nicknames = "")
        : this(fullName, nicknames,
            fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Concat(Lines(nicknames)).Distinct(),
            caseSensitive, elongated: true)
    {
    }

    private NameMatcher(string source, string nicknames, IEnumerable<string> parts, bool caseSensitive, bool elongated)
    {
        Source = source;
        Nicknames = nicknames;
        CaseSensitive = caseSensitive;
        this.parts = [.. parts.Select(p => p.Replace(CurlyApostrophe, '\''))];
        comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        runs = elongated ? [.. this.parts.Select(Runs)] : null;
    }

    /// <summary>
    /// Matches any of the words or phrases, one per line, each as a whole: "good morning" matches
    /// "Good morning!" but not "good" alone. Runs of whitespace inside a phrase, in the list or in chat text,
    /// count as one space; blank lines are skipped.
    /// </summary>
    public static NameMatcher ForPhrases(string lines, bool caseSensitive) =>
        new(lines, "", Lines(lines).Distinct(), caseSensitive, elongated: false);

    // One entry per non-blank line, with runs of spaces inside it made one
    private static IEnumerable<string> Lines(string lines) =>
        lines.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(phrase => phrase.Length > 0);

    public bool IsMatch(ReadOnlySpan<char> text) => Scan(text, null);

    /// <summary>
    /// Adds every whole-word match in <paramref name="text"/> to <paramref name="matches"/>, as
    /// (start, length) in no particular order; matches of different words or phrases may overlap.
    /// A possessive 's is not part of the match.
    /// </summary>
    public void FindAll(ReadOnlySpan<char> text, List<(int Start, int Length)> matches) => Scan(text, matches);

    // With no list, stops at the first match
    private bool Scan(ReadOnlySpan<char> text, List<(int Start, int Length)>? matches)
    {
        // Chat text may use the typographic apostrophe where the name has a straight one, or the
        // reverse; normalising both sides keeps every index the same, so the word checks still hold
        // Stryker disable once Block : skips the copy when there is nothing to straighten; the copy finds the same
        if (!text.Contains(CurlyApostrophe))
        {
            return Find(text, matches);
        }

        // Stryker disable once Conditional,Equality : the stack and the heap hold the same characters
        Span<char> straight = text.Length <= 512 ? stackalloc char[text.Length] : new char[text.Length];
        text.Replace(straight, CurlyApostrophe, '\'');
        return Find(straight, matches);
    }

    private bool Find(ReadOnlySpan<char> text, List<(int Start, int Length)>? matches)
    {
        var any = false;
        for (var p = 0; p < parts.Length; p++)
        {
            var part = parts[p];
            // A phrase is found by its first word, since the spaces after it may be longer in the text
            var space = part.IndexOf(' ');
            // Stryker disable once Equality : no part starts with a space
            var firstWord = part.AsSpan(0, space < 0 ? part.Length : space);
            var offset = 0;
            while (
                // Stryker disable once Equality : at the end of the text, IndexOf finds nothing
                offset < text.Length)
            {
                // A stretched or spelled-out word is found by its first letter, in any case for spelled-out words
                // Stryker disable once Conditional : a phrase found by its first letter alone is still checked whole
                var found = runs == null
                    ? text[offset..].IndexOf(firstWord, comparison)
                    : text[offset..].IndexOf(part.AsSpan(0, 1), StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    break;
                }

                var start = offset + found;
                var end = runs == null ? MatchWritten(text, start, part) : MatchElongated(text, start, part, runs[p]);
                if (
                    // Stryker disable once Equality : a match is never empty, so end is -1 or past start
                    end > start
                    && StartsWord(text, start) && EndsWord(text, end))
                {
                    if (matches == null)
                    {
                        return true;
                    }
                    matches.Add((start, end - start));
                    any = true;
                    offset = end;
                }
                else
                {
                    offset = start + 1;
                }
            }
        }
        return any;
    }

    // The end of the part at start as written, each space matching a run of whitespace; -1 if it isn't there
    private int MatchWritten(ReadOnlySpan<char> text, int i, string part)
    {
        for (var k = 0; ;)
        {
            var space = part.IndexOf(' ', k);
            // Stryker disable once Equality : words are never empty, so space is never 0
            var word = part.AsSpan(k, (space < 0 ? part.Length : space) - k);
            if (i + word.Length > text.Length || !text.Slice(i, word.Length).Equals(word, comparison))
            {
                return -1;
            }

            i += word.Length;
            // Stryker disable once Equality : words are never empty, so space is never 0
            if (space < 0)
            {
                return i;
            }

            i = SkipWhitespace(text, i);
            // Stryker disable once Equality : after a word, i is never 0
            if (i < 0)
            {
                return -1;
            }
            k = space + 1;
        }
    }

    // The end of the whitespace run at i; -1 if there is none
    private static int SkipWhitespace(ReadOnlySpan<char> text, int i)
    {
        var end = i;
        while (end < text.Length && char.IsWhiteSpace(text[end]))
        {
            end++;
        }
        return end > i ? end : -1;
    }

    // The end of the part at start, as written, stretched or spelled out; -1 if it isn't there
    private int MatchElongated(ReadOnlySpan<char> text, int start, string part, (int Start, int Length)[] partRuns)
    {
        var end = MatchStretched(text, start, part, partRuns);
        // Stryker disable once Equality : end is -1 or past start, never 0
        return end >= 0 ? end : MatchSpelled(text, start, part);
    }

    // Each letter may repeat: a run of the same letter in the part matches as written, longer to 3 or more
    // copies, or any longer at the end (Aliiice, Alicee), so Pol misses "pool" and Bob misses "boob"
    private int MatchStretched(ReadOnlySpan<char> text, int i, string part, (int Start, int Length)[] partRuns)
    {
        for (var r = 0; r < partRuns.Length; r++)
        {
            var (runStart, runLength) = partRuns[r];
            if (part[runStart] == ' ')
            {
                i = SkipWhitespace(text, i);
                // Stryker disable once Equality : after a word, i is never 0
                if (i < 0)
                {
                    return -1;
                }
                continue;
            }

            if (i + runLength > text.Length || !text.Slice(i, runLength).Equals(part.AsSpan(runStart, runLength), comparison))
            {
                return -1;
            }

            // Extra copies may be in any case, as in "Aaalice"
            var end = i + runLength;
            var letter = part[runStart];
            if (char.IsLetter(letter))
            {
                while (end < text.Length && SameLetter(text[end], letter))
                {
                    end++;
                }
            }

            var copies = end - i;
            if (copies != runLength && copies < 3 && r < partRuns.Length - 1)
            {
                return -1;
            }
            i = end;
        }
        return i;
    }

    // Letters split by single hyphens or spaces, all the same, in any case: A-L-I-C-E, y o u
    private static int MatchSpelled(ReadOnlySpan<char> text, int start, string part)
    {
        var end = start + (2 * part.Length) - 1;
        if (part.Length < 2 || end > text.Length || text[start + 1] is not (' ' or '-'))
        {
            return -1;
        }

        var separator = text[start + 1];
        for (var k = 0; k < part.Length; k++)
        {
            var i = start + (2 * k);
            if (!char.IsLetter(part[k]) || !SameLetter(text[i], part[k]) || (k > 0 && text[i - 1] != separator))
            {
                return -1;
            }
        }

        // The letters must not run on from or into other single letters: "h a l o" doesn't spell "al"
        var before = start >= 2 && text[start - 1] == separator && IsLoneLetter(text, start - 2);
        var after = end + 1 < text.Length && text[end] == separator && IsLoneLetter(text, end + 1);
        return before || after ? -1 : end;
    }

    private static bool IsLoneLetter(ReadOnlySpan<char> text, int i) =>
        IsWordChar(text[i]) && (i == 0 || !IsWordChar(text[i - 1])) && (i + 1 == text.Length || !IsWordChar(text[i + 1]));

    private static bool SameLetter(char a, char b) => char.ToUpperInvariant(a) == char.ToUpperInvariant(b);

    // Runs of the same letter, in any case; every other character is a run of its own
    private static (int Start, int Length)[] Runs(string part)
    {
        var result = new List<(int Start, int Length)>();
        for (var i = 0; i < part.Length;)
        {
            var end = i + 1;
            if (char.IsLetter(part[i]))
            {
                while (end < part.Length && SameLetter(part[end], part[i]))
                {
                    end++;
                }
            }
            result.Add((i, end - i));
            i = end;
        }
        return [.. result];
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
