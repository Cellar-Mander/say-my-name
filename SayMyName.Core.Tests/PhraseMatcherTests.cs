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
    [InlineData("good  morning")]
    [InlineData("good\tmorning")]
    [InlineData("good \u00A0 morning!")]
    public void MatchesAnyPhraseAsAWhole(string text)
    {
        Assert.True(Words.IsMatch(text));
    }

    [Fact]
    public void HasNoNicknames()
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        // A new matcher, not the shared one, which is built before a mutation run switches a mutant on
        Assert.Equal("", NameMatcher.ForPhrases("raid", caseSensitive: false).Nicknames);
    }

    [Theory]
    [InlineData("")]
    [InlineData("raiders")]
    [InlineData("afraid")]
    [InlineData("good")]
    [InlineData("morning")]
    [InlineData("good mornings")]
    [InlineData("goodmorning")]
    [InlineData("good  mornings")]
    [InlineData("good - morning")]
    [InlineData("good  morn")]
    [InlineData("good evening, morning person")]
    public void IgnoresPartsAndOtherWords(string text)
    {
        // revert-check: not revert-sensitive (guards against over-matching, which the old code never did)
        Assert.False(Words.IsMatch(text));
    }

    [Fact]
    public void FindAllCoversTheWhitespaceRun()
    {
        var matches = new List<(int Start, int Length)>();
        Words.FindAll("oh good   morning!", matches);

        Assert.Equal([(3, 14)], matches);
    }

    [Fact]
    public void LaterWordsMustStillMatchAfterAWhitespaceRun()
    {
        var matcher = NameMatcher.ForPhrases("go go go", caseSensitive: false);
        Assert.True(matcher.IsMatch("go  go   go"));
        Assert.False(matcher.IsMatch("go  go"));
        Assert.False(matcher.IsMatch("go  go  gone"));
    }

    // A one-letter first word puts a non-letter right after a failed match's first character
    [Theory]
    [InlineData("a  b", true)]
    [InlineData("a  c", false)]
    [InlineData("a,b", false)]
    [InlineData("ab", false)]
    public void OneLetterWordsStillNeedTheWholePhrase(string text, bool expected)
    {
        Assert.Equal(expected, NameMatcher.ForPhrases("a b", caseSensitive: false).IsMatch(text));
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
