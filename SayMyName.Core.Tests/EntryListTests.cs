using System.Text;
using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class EntryListTests
{
    [Fact]
    public void SplitTrimsEntriesAndDropsBlankLines()
    {
        // As an earlier release's text box could save it
        Assert.Equal(["Zuzu", "Big  Zed"], EntryList.Split("\n  Zuzu \r\n\n  \nBig  Zed\r\n"));
    }

    [Fact]
    public void SplitOfEmptyListIsEmpty()
    {
        Assert.Empty(EntryList.Split(""));
    }

    [Fact]
    public void JoinRoundTripsThroughSplit()
    {
        string[] entries = ["raid", "good morning"];

        Assert.Equal(entries, EntryList.Split(EntryList.Join(entries)));
    }

    [Theory]
    [InlineData("abc", 5, 3)]
    [InlineData("abcdef", 3, 3)]
    [InlineData("abc", 3, 3)]
    // Two-byte é, three-byte あ: the cut falls after whole characters, not after maxChars bytes
    [InlineData("éé", 1, 2)]
    [InlineData("ああa", 2, 6)]
    // A surrogate pair counts as two characters, as string.Length does, and is never split
    [InlineData("a😀", 2, 1)]
    [InlineData("a😀", 3, 5)]
    public void Utf8PrefixLengthKeepsWholeCharacters(string text, int maxChars, int expectedBytes)
    {
        Assert.Equal(expectedBytes, EntryList.Utf8PrefixLength(Encoding.UTF8.GetBytes(text), maxChars));
    }
}
