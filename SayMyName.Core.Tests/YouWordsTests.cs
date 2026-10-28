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
    [InlineData("y o u")]
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
