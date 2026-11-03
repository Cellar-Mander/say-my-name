using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class YouWordsTests
{
    [Theory]
    [InlineData("you")]
    [InlineData("thank you")]
    [InlineData("is this YOUR chocobo?")]
    [InlineData("is this yours?")]
    [InlineData("you're late")]
    [InlineData("you’re late")]
    [InlineData("Hey, you.")]
    [InlineData("youuu")]
    [InlineData("thank youuu")]
    [InlineData("yooou")]
    [InlineData("YOURRR turn")]
    [InlineData("y o u")]
    [InlineData("Y-O-U-R-S")]
    [InlineData("y-you")]
    [InlineData("t-thank y-y-you")]
    [InlineData("youuu're late")]
    [InlineData("youre late")]
    [InlineData("you'll see")]
    [InlineData("you’ll see")]
    [InlineData("YOULL see")]
    [InlineData("you've won")]
    [InlineData("You’ve won")]
    [InlineData("youve won")]
    [InlineData("you'd know")]
    [InlineData("you’d know")]
    [InlineData("Youd know")]
    [InlineData("y'all")]
    [InlineData("Y’ALL ready?")]
    [InlineData("yall ready?")]
    [InlineData("yaaall ready?")]
    [InlineData("see yous later")]
    [InlineData("YOUS")]
    [InlineData("help yourself")]
    [InlineData("YourSelves")]
    [InlineData("thou art")]
    [InlineData("THOU")]
    [InlineData("thee")]
    [InlineData("Theee")]
    [InlineData("thy blade")]
    [InlineData("Thy")]
    [InlineData("it is thine")]
    [InlineData("THINE")]
    public void MatchesYouWords(string text)
    {
        Assert.True(YouWords.IsMatch(text));
    }

    // revert-check: not revert-sensitive (near misses of each new You word, which the old list also rejected)
    [Theory]
    [InlineData("")]
    [InlineData("hello there")]
    [InlineData("youth")]
    [InlineData("bayou")]
    [InlineData("yoou")]
    [InlineData("yesterday")]
    [InlineData("the end")]
    [InlineData("tell them")]
    [InlineData("over there")]
    [InlineData("though")]
    [InlineData("thing")]
    [InlineData("thyme")]
    [InlineData("yourselfish")]
    [InlineData("youthful")]
    [InlineData("yallow")]
    [InlineData("y o u t h")]
    public void IgnoresOtherWords(string text)
    {
        Assert.False(YouWords.IsMatch(text));
    }

    [Fact]
    public void FindAllGivesEachYouWord()
    {
        var matches = new List<(int Start, int Length)>();
        YouWords.FindAll("thank youuu, is this yours? You’re y'all? thee", matches);
        Assert.Equal([(6, 5), (21, 5), (28, 6), (35, 5), (42, 4)], matches.Order());
    }

    [Fact]
    public void FindAllGivesTheWholeLongerWord()
    {
        var matches = new List<(int Start, int Length)>();
        YouWords.FindAll("help yourself, y’all", matches);
        Assert.Equal([(5, 8), (15, 5)], matches.Order());
    }
}
