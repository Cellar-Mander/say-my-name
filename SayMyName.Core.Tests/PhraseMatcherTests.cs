using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class PhraseMatcherTests
{
    private static readonly NameMatcher Words = NameMatcher.ForPhrases("raid\n  good   morning \n\nY'shtola\r\n", caseSensitive: false);

    [Theory]
    [InlineData("raid tonight?")]
    [InlineData("RAID")]
    [InlineData("the raid's at 9")]
    [InlineData("Good morning!")]
    [InlineData("well, good morning to you")]
    [InlineData("ask Y’shtola")]
    public void MatchesAnyPhraseAsAWhole(string text)
    {
        Assert.True(Words.IsMatch(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("raiders")]
    [InlineData("afraid")]
    [InlineData("good")]
    [InlineData("morning")]
    [InlineData("good mornings")]
    [InlineData("goodmorning")]
    [InlineData("good evening, morning person")]
    public void IgnoresPartsAndOtherWords(string text)
    {
        Assert.False(Words.IsMatch(text));
    }

    [Fact]
    public void CaseSensitiveMatchesOnlyAsWritten()
    {
        var matcher = NameMatcher.ForPhrases("Raid", caseSensitive: true);
        Assert.True(matcher.IsMatch("Raid night"));
        Assert.False(matcher.IsMatch("raid night"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n  \r\n\t\n")]
    public void NoPhrasesMatchNothing(string lines)
    {
        var matcher = NameMatcher.ForPhrases(lines, caseSensitive: false);
        Assert.False(matcher.IsMatch("anything at all"));
        Assert.False(matcher.IsMatch(" "));
    }
}
