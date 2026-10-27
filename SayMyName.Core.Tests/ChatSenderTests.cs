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
    public void OtherNamesAreNotYou(string sender)
    {
        Assert.False(ChatSender.Is(sender, "Rose Thorne"));
    }

    [Fact]
    public void KeepsApostrophesAndHyphensInsideTheName()
    {
        Assert.Equal("A'ra'ka Mary-Jane", ChatSender.Name("A'ra'ka Mary-Jane").ToString());
    }

    [Fact]
    public void NoCharacterNameMatchesNoSender()
    {
        Assert.False(ChatSender.Is("", ""));
        Assert.False(ChatSender.Is("★", ""));
    }
}
