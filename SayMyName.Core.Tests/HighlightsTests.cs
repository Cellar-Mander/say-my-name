using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class HighlightsTests
{
    private static List<(int Start, int Length)> Merged(params (int Start, int Length)[] matches)
    {
        var list = matches.ToList();
        Highlights.Merge(list);
        return list;
    }

    private static List<(int Start, int Length, bool Highlighted)> Split(int start, int length, params (int Start, int Length)[] matches)
    {
        var pieces = new List<(int Start, int Length, bool Highlighted)>();
        Highlights.Split(start, length, Merged(matches), pieces);
        return pieces;
    }

    [Fact]
    public void MergeSortsAndJoinsOverlappingAndTouching()
    {
        Assert.Equal([(0, 4), (6, 7)], Merged((8, 5), (0, 4), (6, 3), (9, 2)));
        Assert.Equal([(0, 6)], Merged((3, 3), (0, 3)));
        Assert.Equal([(2, 5)], Merged((2, 5), (3, 1)));
    }

    [Fact]
    public void MergeKeepsSeparateMatchesApart()
    {
        Assert.Equal([(0, 2), (3, 2)], Merged((3, 2), (0, 2)));
        Assert.Empty(Merged());
    }

    [Fact]
    public void SplitsAroundMatchesInsideTheSegment()
    {
        // "hi Rose and Rose!" as one payload
        Assert.Equal(
            [(0, 3, false), (3, 4, true), (7, 5, false), (12, 4, true), (16, 1, false)],
            Split(0, 17, (3, 4), (12, 4)));
    }

    [Fact]
    public void SegmentPositionsAreRelativeToTheSegment()
    {
        // Text "<link>hi Rose" where the link's text is 4 characters, so this payload starts at 4
        Assert.Equal([(0, 3, false), (3, 4, true)], Split(4, 7, (7, 4)));
    }

    [Fact]
    public void SegmentWithoutMatchesIsOnePlainPiece()
    {
        Assert.Equal([(0, 5, false)], Split(10, 5, (0, 4), (20, 3)));
        Assert.Equal([(0, 5, false)], Split(0, 5));
    }

    [Fact]
    public void MatchCrossingSegmentEdgesIsCutToEachSegment()
    {
        // "good morning" split across payloads "say good" and " morning!"
        Assert.Equal([(0, 4, false), (4, 4, true)], Split(0, 8, (4, 12)));
        Assert.Equal([(0, 8, true), (8, 1, false)], Split(8, 9, (4, 12)));
    }

    [Fact]
    public void WholeSegmentMatchedIsOneHighlightedPiece()
    {
        Assert.Equal([(0, 4, true)], Split(3, 4, (3, 4)));
    }

    [Fact]
    public void EmptySegmentHasNoPieces()
    {
        Assert.Empty(Split(3, 0, (0, 10)));
    }
}
