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
    [InlineData("A'ra'ka")]
    [InlineData("hi A'ra'ka!")]
    [InlineData("A'ra'ka's spear")]
    [InlineData("A’ra’ka")]
    [InlineData("A'ra’ka")]
    [InlineData("A’ra'ka’s spear")]
    [InlineData("Tia")]
    public void NamesWithSeveralApostrophesMatchWhole(string text)
    {
        Assert.True(new NameMatcher("A'ra'ka Tia", caseSensitive: true).IsMatch(text));
    }

    [Theory]
    [InlineData("A'ra")]
    [InlineData("Ra'ka")]
    [InlineData("ra'ka")]
    [InlineData("A'raka")]
    [InlineData("A'ra'kan")]
    [InlineData("A'ra'ka'ni")]
    [InlineData("A’ra’ka’ni")]
    [InlineData("X'A'ra'ka")]
    [InlineData("A'Ra'Ka")]
    public void PiecesOrExtensionsOfAnApostropheNameDoNotMatch(string text)
    {
        Assert.False(new NameMatcher("A'ra'ka Tia", caseSensitive: true).IsMatch(text));
    }

    [Fact]
    public void ApostrophesInTheNameItselfMayBeEitherKind()
    {
        Assert.True(new NameMatcher("A’ra’ka Tia", caseSensitive: true).IsMatch("A'ra'ka"));
    }

    [Fact]
    public void CaseInsensitiveMatchesAnyCaseAroundApostrophes()
    {
        Assert.True(new NameMatcher("A'ra'ka Tia", caseSensitive: false).IsMatch("A’RA’KA"));
    }

    [Fact]
    public void LongTextWithACurlyApostropheStillMatches()
    {
        var text = new string('x', 600) + " Rose’s";
        Assert.True(Rose.IsMatch(text));
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

    [Theory]
    [InlineData("Roseee")]
    [InlineData("Rosee!")]
    [InlineData("Rooose")]
    [InlineData("Rooooseeee")]
    [InlineData("Rrrose")]
    [InlineData("Roseee's chocobo")]
    [InlineData("Thoooorne")]
    [InlineData("R-O-S-E")]
    [InlineData("r o s e")]
    [InlineData("hey r-o-s-e!")]
    [InlineData("I see R O S E")]
    public void MatchesStretchedOrSpelledOutNames(string text)
    {
        Assert.True(Rose.IsMatch(text));
    }

    // revert-check: not revert-sensitive (near misses the old matcher also rejected; guards against over-matching)
    [Theory]
    [InlineData("Roose")]
    [InlineData("Rosse")]
    [InlineData("ROOOSE")]
    [InlineData("Roseees")]
    [InlineData("Roseee'ka")]
    [InlineData("Rose-y")]
    [InlineData("R-O-S-E-S")]
    [InlineData("R O S E S")]
    [InlineData("a R O S E")]
    [InlineData("R-O S-E")]
    [InlineData("R  O  S  E")]
    [InlineData("R-O-S")]
    [InlineData("RO-SE")]
    public void IgnoresStretchesInsideWordsAndPartSpellings(string text)
    {
        Assert.False(Rose.IsMatch(text));
    }

    // revert-check: not revert-sensitive (near misses the old matcher also rejected; guards against over-matching)
    [Theory]
    [InlineData("Pol Bob", "Pool")]
    [InlineData("Pol Bob", "Boob")]
    [InlineData("Anna Lee", "Ana")]
    [InlineData("Anna Lee", "Le")]
    public void DoubledLettersAreNotAStretch(string name, string text)
    {
        Assert.False(new NameMatcher(name, caseSensitive: true).IsMatch(text));
    }

    [Theory]
    [InlineData("Pol Bob", "Poool")]
    [InlineData("Pol Bob", "Bobbb")]
    [InlineData("Anna Lee", "Annna")]
    [InlineData("Anna Lee", "Leeeee")]
    [InlineData("Anna Lee", "A-N-N-A")]
    [InlineData("Y'shtola Rhul", "Y'shtolaaa")]
    [InlineData("Mary-Jane Doe", "Maaary-Jane")]
    public void LongerStretchesMatch(string name, string text)
    {
        Assert.True(new NameMatcher(name, caseSensitive: true).IsMatch(text));
    }

    [Fact]
    public void CaseInsensitiveMatchesStretchesInAnyCase()
    {
        Assert.True(new NameMatcher("Rose Thorne", caseSensitive: false).IsMatch("ROOOSEEE"));
    }

    // revert-check: not revert-sensitive (phrases never matched stretches; guards against stretching leaking into phrases)
    [Theory]
    [InlineData("raiiid")]
    [InlineData("raidd")]
    [InlineData("r-a-i-d")]
    public void PhrasesMatchOnlyAsWritten(string text)
    {
        Assert.False(NameMatcher.ForPhrases("raid", caseSensitive: false).IsMatch(text));
    }

    private static List<(int Start, int Length)> FindAll(NameMatcher matcher, string text)
    {
        var matches = new List<(int Start, int Length)>();
        matcher.FindAll(text, matches);
        return [.. matches.Order()];
    }

    [Fact]
    public void FindAllFindsEveryWholeWordMatch()
    {
        Assert.Equal([(0, 4), (5, 6), (16, 4)], FindAll(Rose, "Rose Thorne and Rose"));
    }

    [Fact]
    public void FindAllSkipsPartWordsAndLeavesOffThePossessive()
    {
        Assert.Equal([(14, 4)], FindAll(Rose, "Primrose, ask Rose's Roses"));
    }

    [Fact]
    public void FindAllKeepsPositionsWithCurlyApostrophes()
    {
        Assert.Equal([(4, 8)], FindAll(new NameMatcher("Y'shtola Rhul", caseSensitive: true), "ask Y’shtola"));
    }

    [Fact]
    public void FindAllReportsOverlappingPhrases()
    {
        var matcher = NameMatcher.ForPhrases("good morning\nmorning", caseSensitive: false);
        Assert.Equal([(0, 12), (5, 7)], FindAll(matcher, "Good morning!"));
    }

    [Fact]
    public void FindAllGivesTheWholeStretchedOrSpelledWord()
    {
        Assert.Equal([(3, 9), (14, 7), (26, 7)], FindAll(Rose, "hi Rooooseee, R-O-S-E and R O S E's"));
    }

    [Fact]
    public void FindAllOnNoMatchAddsNothing()
    {
        Assert.Empty(FindAll(Rose, "a rose by any other name"));
        Assert.Empty(FindAll(new NameMatcher("", caseSensitive: true), "Rose"));
    }
}
