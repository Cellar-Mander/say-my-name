using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class ChatSenderTests
{
    [Theory]
    [InlineData("Rose Thorne")]
    [InlineData("Rose Thorne")]
    [InlineData("(1)Rose Thorne")]
    [InlineData("★Rose Thorne")]
    [InlineData(" Rose Thorne ")]
    [InlineData("Rose Thorne")]
    // Tags other plugins add: Party Icons' job prefix (after the party number it keeps), Player Tags before or after
    [InlineData("WAR Rose Thorne")]
    [InlineData("WAR Rose Thorne")]
    [InlineData("Rose Thorne Mentor")]
    [InlineData("Healer Rose Thorne Friend")]
    public void RecognisesTheNameInsideTheGamesMarks(string sender)
    {
        Assert.True(ChatSender.Is(sender, "Rose Thorne"));
    }

    [Theory]
    [InlineData("Rose Thornet")]
    [InlineData("Primrose Thorne")]
    [InlineData("rose thorne")]
    [InlineData("Rose")]
    [InlineData("")]
    [InlineData("WAR Rose Thornet")]
    [InlineData("WARRose Thorne")]
    [InlineData("Rose ThorneRose Thornet")]
    public void OtherNamesAreNotYou(string sender)
    {
        // revert-check: not revert-sensitive (guards against the tag-tolerant match accepting names a letter touches)
        Assert.False(ChatSender.Is(sender, "Rose Thorne"));
    }

    [Fact]
    public void KeepsApostrophesAndHyphensInsideTheName()
    {
        Assert.Equal("A'ra'ka Mary-Jane", ChatSender.Name("A'ra'ka Mary-Jane").ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("★(1)")]
    public void SenderTextWithoutLettersHasNoName(string sender)
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        Assert.Equal("", ChatSender.Name(sender).ToString());
    }

    [Fact]
    public void NoCharacterNameMatchesNoSender()
    {
        Assert.False(ChatSender.Is("", ""));
        Assert.False(ChatSender.Is("★", ""));
    }
}
