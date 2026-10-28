namespace SayMyName.Core;

/// <summary>
/// Finds a word addressing the reader (you, your, yours, yourself, you're, you'll, y'all, thou, thee and the like) in chat text,
/// in any case, stretched or spelled out, with or without the apostrophe.
/// </summary>
public static class YouWords
{
    // NameMatcher's whole-word rules suit these too: "you're", "you’re", "youuu" and "y o u" match, "youth" and "bayou" don't
    private static readonly NameMatcher Matcher = new(
        "you your yours yous yourself yourselves you're youre you'll youll you've youve you'd youd y'all yall thou thee thy thine",
        caseSensitive: false);

    public static bool IsMatch(ReadOnlySpan<char> text) => Matcher.IsMatch(text);

    /// <inheritdoc cref="NameMatcher.FindAll"/>
    public static void FindAll(ReadOnlySpan<char> text, List<(int Start, int Length)> matches) => Matcher.FindAll(text, matches);
}
