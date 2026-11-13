using SayMyName.Core;

namespace SayMyName.Core.Tests;

// Examples follow the rows of the Name tables in docs/design.md where one fits
public class NameMatcherTests
{
    private static readonly NameMatcher Alice = new("Alice Smith", caseSensitive: true);

    [Theory]
    [InlineData("Alice")]
    [InlineData("Smith")]
    [InlineData("hi Alice")]
    [InlineData("Alice!")]
    [InlineData("Alice Smith")]
    [InlineData("is that Alice?")]
    [InlineData("(Alice)")]
    [InlineData("\"Alice\"")]
    [InlineData("Alice's sword")]
    [InlineData("Alice’s sword")]
    [InlineData("Smith's")]
    [InlineData("ask aliceberry or Alice")]
    public void MatchesFirstOrLastNameAsWholeWord(string text)
    {
        Assert.True(Alice.IsMatch(text));
    }

    [Theory]
    // Quote marks around a name, and a hyphen or dash cutting it off, don't join it to anything
    [InlineData("call me 'Alice'")]
    [InlineData("hey Alice-")]
    [InlineData("Alice- wait")]
    [InlineData("Alice-- wait")]
    [InlineData("Alice—")]
    [InlineData("Alice— wait")]
    [InlineData("Alice—wait")]
    [InlineData("Alice–wait")]
    [InlineData("—Alice—")]
    public void QuotedOrCutOffNamesMatch(string text)
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        Assert.True(Alice.IsMatch(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("alice")]
    [InlineData("ALICE")]
    [InlineData("Alices are here")]
    [InlineData("Malice")]
    [InlineData("Alicey")]
    [InlineData("Alice2")]
    [InlineData("Alice'ka")]
    [InlineData("Mary-Alice")]
    [InlineData("Alice-Ann")]
    [InlineData("O'Alice")]
    [InlineData("Alice'sa")]
    [InlineData("Smit")]
    public void IgnoresOtherWordsAndOtherCase(string text)
    {
        Assert.False(Alice.IsMatch(text));
    }

    [Theory]
    [InlineData("alice")]
    [InlineData("ALICE")]
    [InlineData("ALICE'S")]
    [InlineData("smith")]
    public void CaseInsensitiveMatchesAnyCase(string text)
    {
        Assert.True(new NameMatcher("Alice Smith", caseSensitive: false).IsMatch(text));
    }

    private static readonly NameMatcher AliceAndNicknames = new("Alice Smith", caseSensitive: true, nicknames: "Zuzu\n\n  Big   Zed \r\nSmith\n");

    [Theory]
    [InlineData("Zuzu")]
    [InlineData("hey Zuzu!")]
    [InlineData("Zuzu's chocobo")]
    [InlineData("Zuzu’s chocobo")]
    [InlineData("Zuzuuu!")]
    [InlineData("Z-U-Z-U")]
    [InlineData("Big Zed")]
    [InlineData("Big Zed's?")]
    [InlineData("Alice")]
    public void NicknamesMatchLikeNames(string text)
    {
        Assert.True(AliceAndNicknames.IsMatch(text));
    }

    [Theory]
    [InlineData("zuzu")]
    [InlineData("Zuzus")]
    [InlineData("Zuzu'ka")]
    [InlineData("Big")]
    [InlineData("Zed")]
    [InlineData("Big Zedo")]
    [InlineData("BigZed")]
    public void NicknamesNeedWholeWordsAndCase(string text)
    {
        // revert-check: not revert-sensitive (guards against over-matching, which the old code never did)
        Assert.False(AliceAndNicknames.IsMatch(text));
    }

    [Fact]
    public void NicknamesFollowCaseSetting()
    {
        Assert.True(new NameMatcher("Alice Smith", caseSensitive: false, nicknames: "Zuzu").IsMatch("hey zuzu"));
    }

    [Fact]
    public void NicknamesWorkWithoutACharacterName()
    {
        Assert.True(new NameMatcher("", caseSensitive: true, nicknames: "Zuzu").IsMatch("hey Zuzu"));
    }

    [Fact]
    public void FindAllReportsNicknamesWithoutPossessive()
    {
        var matches = new List<(int Start, int Length)>();
        AliceAndNicknames.FindAll("ask Big Zed's friend", matches);

        Assert.Equal([(4, 7)], matches);
    }

    [Theory]
    // Japanese puts no space between a name and an honorific or particle
    [InlineData("Aliceさん、こんにちは")]
    [InlineData("Aliceが来た")]
    [InlineData("Alice様")]
    [InlineData("Aliceちゃん")]
    [InlineData("Aliceｻﾝ")]
    [InlineData("Aliceー")]
    [InlineData("こんにちはAlice")]
    [InlineData("おはようAliceさん")]
    [InlineData("Aliiiceさん")]
    [InlineData("A-L-I-C-Eさん")]
    [InlineData("Alice'sです")]
    public void MatchesNamesNextToJapaneseText(string text)
    {
        Assert.True(Alice.IsMatch(text));
    }

    [Theory]
    // The first and last character of each Japanese block
    [InlineData("\u3005")]
    [InlineData("\u3006")]
    [InlineData("\u3041")]
    [InlineData("\u30FF")]
    [InlineData("\u31F0")]
    [InlineData("\u31FF")]
    [InlineData("\u3400")]
    [InlineData("\u4DBF")]
    [InlineData("\u4E00")]
    [InlineData("\u9FFF")]
    [InlineData("\uF900")]
    [InlineData("\uFAD9")]
    [InlineData("\uFF66")]
    [InlineData("\uFF9F")]
    public void EveryJapaneseBlockEndsAName(string japanese)
    {
        Assert.True(Alice.IsMatch("Alice" + japanese));
    }

    [Theory]
    [InlineData("Aliceyさん")]
    [InlineData("Malice様")]
    [InlineData("Alice2さん")]
    public void JapaneseTextDoesNotMakeOtherWordsMatch(string text)
    {
        Assert.False(Alice.IsMatch(text));
    }

    [Fact]
    public void JapaneseNicknamesMatchBeforeParticles()
    {
        var matcher = new NameMatcher("Alice Smith", caseSensitive: true, nicknames: "アリス");
        var matches = new List<(int Start, int Length)>();
        matcher.FindAll("アリスさん、Aliceが来た", matches);

        Assert.Equal([(6, 5), (0, 3)], matches);
    }

    [Theory]
    [InlineData("Big  Zed", true)]
    [InlineData("hi Biiig\tZeddd!", true)]
    [InlineData("Big   Zed's", true)]
    [InlineData("Big  zed", false)]
    [InlineData("BigZed", false)]
    [InlineData("Big - Zed", false)]
    public void MultiWordNicknamesMatchAcrossWhitespaceRuns(string text, bool expected)
    {
        Assert.Equal(expected, new NameMatcher("", caseSensitive: true, nicknames: "Big Zed").IsMatch(text));
    }

    [Theory]
    [InlineData("A  B", true)]
    [InlineData("A,B", false)]
    public void OneLetterNicknameWordsNeedTheWhitespace(string text, bool expected)
    {
        Assert.Equal(expected, new NameMatcher("", caseSensitive: true, nicknames: "A B").IsMatch(text));
    }

    [Fact]
    public void FindAllReportsNicknamesAcrossAWhitespaceRun()
    {
        var matches = new List<(int Start, int Length)>();
        new NameMatcher("", caseSensitive: true, nicknames: "Big Zed").FindAll("ask Big  Zed's friend", matches);

        Assert.Equal([(4, 8)], matches);
    }

    [Fact]
    public void CaseInsensitiveStillNeedsWholeWords()
    {
        Assert.False(new NameMatcher("Alice Smith", caseSensitive: false).IsMatch("malices"));
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
        var text = new string('x', 600) + " Alice’s";
        Assert.True(Alice.IsMatch(text));
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
        Assert.True(Alice.IsMatch("Alices for Alice"));
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
    [InlineData("Aliceee")]
    [InlineData("Alicee")]
    [InlineData("Alicee!")]
    [InlineData("Aliiice")]
    [InlineData("Aliiiiceeee")]
    [InlineData("Aaalice")]
    [InlineData("Aliceee's sword")]
    [InlineData("Smiiiith")]
    [InlineData("A-L-I-C-E")]
    [InlineData("a l i c e")]
    [InlineData("hey a-l-i-c-e!")]
    [InlineData("I see A L I C E")]
    [InlineData("a l i c e is here")]
    [InlineData("hi a l i c e ")]
    // Spelled out, the first letter may repeat any number of times, the others as when stretched
    [InlineData("A-A-L-I-C-E")]
    [InlineData("a a a l i c e")]
    [InlineData("A-L-I-I-I-C-E")]
    [InlineData("A-L-I-C-E-E")]
    [InlineData("A-L-I-C-E-E-E")]
    // A stutter on the first letter before the name as written or stretched
    [InlineData("A-Alice")]
    [InlineData("a-A-Alice!")]
    [InlineData("a-A-Aliiice")]
    [InlineData("A-Aliiiceee")]
    public void MatchesStretchedOrSpelledOutNames(string text)
    {
        Assert.True(Alice.IsMatch(text));
    }

    // revert-check: not revert-sensitive (near misses the old matcher also rejected; guards against over-matching)
    [Theory]
    [InlineData("Aliice")]
    [InlineData("Alicce")]
    [InlineData("ALIIICE")]
    [InlineData("Aliceees")]
    [InlineData("Aliceee'ka")]
    [InlineData("Alice-y")]
    [InlineData("A-L-I-C-E-S")]
    [InlineData("A L I C E S")]
    [InlineData("h a l i c e")]
    [InlineData("A-L I-C-E")]
    [InlineData("A  L  I  C  E")]
    [InlineData("A-L-I")]
    [InlineData("AL-ICE")]
    public void IgnoresStretchesInsideWordsAndPartSpellings(string text)
    {
        Assert.False(Alice.IsMatch(text));
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
    // A single letter right next to a spelling isn't in the name, whatever is between them
    [InlineData("I A-L-I-C-E")]
    [InlineData("I,A-L-I-C-E")]
    [InlineData("A A-L-I-C-E")]
    [InlineData("A-L-I-C-E I")]
    [InlineData("A-L-I-C-E/I")]
    [InlineData("A-A L-I-C-E")]
    public void LoneLetterNextToASpellingStopsIt(string text)
    {
        Assert.False(Alice.IsMatch(text));
    }

    [Theory]
    // Past the first letter, a doubled letter is no more a stretch spelled out than written
    [InlineData("A-L-L-I-C-E")]
    [InlineData("a l i c c e")]
    // The stutter needs the first letter, and the name after it still follows Match case
    [InlineData("L-Alice")]
    [InlineData("A-alice")]
    [InlineData("A-Aliice")]
    public void IgnoresDoubledSpelledLettersAndOtherStutters(string text)
    {
        Assert.False(Alice.IsMatch(text));
    }

    [Fact]
    public void FindAllGivesTheWholeStutter()
    {
        var matches = new List<(int Start, int Length)>();
        Alice.FindAll("hi a-A-Aliiice!", matches);
        Assert.Equal([(3, 11)], matches);

        // A stutter needs hyphens: with a space, only the name is coloured
        matches.Clear();
        Alice.FindAll("A Alice", matches);
        Assert.Equal([(2, 5)], matches);
    }

    [Fact]
    public void DoubledLetterIsNotAStretchAfterAOneLetterWord()
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        Assert.False(new NameMatcher("", caseSensitive: true, nicknames: "J Bob").IsMatch("J Boob"));
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
        Assert.True(new NameMatcher("Alice Smith", caseSensitive: false).IsMatch("ALIIICEEE"));
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
        Assert.Equal([(0, 5), (6, 5), (16, 5)], FindAll(Alice, "Alice Smith and Alice"));
    }

    [Fact]
    public void FindAllSkipsPartWordsAndLeavesOffThePossessive()
    {
        Assert.Equal([(12, 5)], FindAll(Alice, "Malice, ask Alice's Alices"));
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
        Assert.Equal([(3, 7), (12, 9), (26, 9)], FindAll(Alice, "hi Aliiice, A-L-I-C-E and A L I C E's"));
    }

    [Theory]
    // A dash cutting the name off isn't coloured
    [InlineData("Alice—", 5)]
    [InlineData("Alice—wait", 5)]
    [InlineData("Alice- wait", 5)]
    // A spelling with repeated letters is coloured whole
    [InlineData("A-A-L-I-C-E", 11)]
    [InlineData("A-L-I-I-I-C-E", 13)]
    [InlineData("A-L-I-C-E-E", 11)]
    public void FindAllColoursCutOffNamesAndRepeatedSpellings(string text, int length)
    {
        Assert.Equal([(0, length)], FindAll(Alice, text));
    }

    [Fact]
    public void FindAllOnNoMatchAddsNothing()
    {
        Assert.Empty(FindAll(Alice, "alice in wonderland"));
        Assert.Empty(FindAll(new NameMatcher("", caseSensitive: true), "Alice"));
    }
}
