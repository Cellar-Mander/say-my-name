namespace SayMyName.Core;

/// <summary>Finds a word addressing the reader (you, your, yours, you're) in chat text, in any case, stretched or spelled out.</summary>
public static class YouWords
{
    // NameMatcher's whole-word rules suit these too: "you're", "you’re", "youuu" and "y o u" match, "youth" and "you'll" don't
    private static readonly NameMatcher Matcher = new("you your yours you're", caseSensitive: false);

    public static bool IsMatch(ReadOnlySpan<char> text) => Matcher.IsMatch(text);

    /// <inheritdoc cref="NameMatcher.FindAll"/>
    public static void FindAll(ReadOnlySpan<char> text, List<(int Start, int Length)> matches) => Matcher.FindAll(text, matches);
}
