using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class NameMatcherTests
{
    private static readonly NameMatcher Rose = new("Rose Thorne", caseSensitive: true);

    [Theory]
    [InlineData("Rose")]
    [InlineData("Thorne")]
    [InlineData("hey Rose, over here")]
    [InlineData("Rose Thorne!")]
    [InlineData("is that Rose?")]
    [InlineData("(Rose)")]
    [InlineData("\"Rose\"")]
    [InlineData("Rose's chocobo")]
    [InlineData("Rose’s chocobo")]
    [InlineData("Thorne's")]
    [InlineData("ask rosemary or Rose")]
    public void MatchesFirstOrLastNameAsWholeWord(string text)
    {
        Assert.True(Rose.IsMatch(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a rose by any other name")]
    [InlineData("ROSE")]
    [InlineData("Roses are red")]
    [InlineData("Primrose")]
    [InlineData("Rosey")]
    [InlineData("Rose2")]
    [InlineData("Rose'ka")]
    [InlineData("Mary-Rose")]
    [InlineData("Rose-Marie")]
    [InlineData("O'Rose")]
    [InlineData("Rose'sa")]
    [InlineData("Thorn")]
    public void IgnoresOtherWordsAndOtherCase(string text)
    {
        Assert.False(Rose.IsMatch(text));
    }

    [Theory]
    [InlineData("a rose by any other name")]
    [InlineData("ROSE")]
    [InlineData("ROSE'S")]
    [InlineData("thorne")]
    public void CaseInsensitiveMatchesAnyCase(string text)
    {
        Assert.True(new NameMatcher("Rose Thorne", caseSensitive: false).IsMatch(text));
    }

    [Fact]
    public void CaseInsensitiveStillNeedsWholeWords()
    {
        Assert.False(new NameMatcher("Rose Thorne", caseSensitive: false).IsMatch("primroses"));
    }

    [Theory]
    [InlineData("Y'shtola Rhul", "ask Y'shtola")]
    [InlineData("Y'shtola Rhul", "Y'shtola's staff")]
    [InlineData("Mary-Jane Doe", "Mary-Jane!")]
    public void NamesWithApostrophesAndHyphensMatchWhole(string name, string text)
    {
        Assert.True(new NameMatcher(name, caseSensitive: true).IsMatch(text));
    }

    [Theory]
    [InlineData("Mary-Jane Doe", "Mary")]
    [InlineData("Mary-Jane Doe", "Jane")]
    public void PartOfAHyphenatedNameIsNotTheName(string name, string text)
    {
        Assert.False(new NameMatcher(name, caseSensitive: true).IsMatch(text));
    }

    [Fact]
    public void LaterOccurrenceMatchesAfterAnEarlierNonWordOne()
    {
        Assert.True(Rose.IsMatch("Roses for Rose"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyNameMatchesNothing(string name)
    {
        var matcher = new NameMatcher(name, caseSensitive: false);
        Assert.False(matcher.IsMatch("anything at all"));
        Assert.False(matcher.IsMatch(""));
    }
}
