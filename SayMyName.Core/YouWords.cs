namespace SayMyName.Core;

/// <summary>Finds a word addressing the reader (you, your, yours, you're) in chat text, in any case.</summary>
public static class YouWords
{
    // NameMatcher's whole-word rules suit these too: "you're" and "you’re" match, "youth" and "you'll" don't
    private static readonly NameMatcher Matcher = new("you your yours you're", caseSensitive: false);

    public static bool IsMatch(ReadOnlySpan<char> text) => Matcher.IsMatch(text);
}
