using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class YouWordsTests
{
    [Theory]
    [InlineData("you")]
    [InlineData("Thank you!")]
    [InlineData("is this YOUR chocobo?")]
    [InlineData("it's yours")]
    [InlineData("you're late")]
    [InlineData("you’re late")]
    [InlineData("Hey, you.")]
    [InlineData("youuu")]
    [InlineData("thank youu")]
    [InlineData("yooou")]
    [InlineData("YOURRR turn")]
    [InlineData("y o u")]
    [InlineData("Y-O-U-R-S")]
    [InlineData("youuu're late")]
    public void MatchesYouWords(string text)
    {
        Assert.True(YouWords.IsMatch(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("hello there")]
    [InlineData("youth")]
    [InlineData("bayou")]
    [InlineData("you'll see")]
    [InlineData("yourself")]
    [InlineData("yoou")]
    [InlineData("youu'll")]
    [InlineData("y o u t h")]
    public void IgnoresOtherWords(string text)
    {
        Assert.False(YouWords.IsMatch(text));
    }

    [Fact]
    public void FindAllGivesEachYouWord()
    {
        var matches = new List<(int Start, int Length)>();
        YouWords.FindAll("Thank you, is this yours? You’re kind", matches);
        Assert.Equal([(6, 3), (19, 5), (26, 6)], matches.Order());
    }
}
