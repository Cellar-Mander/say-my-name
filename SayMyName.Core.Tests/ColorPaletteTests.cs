using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class ColorPaletteTests
{
    private const uint Red = 0xFF0000FF;
    private const uint DarkRed = 0x800000FF;
    private const uint PaleRed = 0xFF8080FF;
    private const uint Green = 0x00FF00FF;
    private const uint Blue = 0x0000FFFF;
    private const uint Magenta = 0xFF00FFFF;
    private const uint Orange = 0xFF8000FF;
    private const uint White = 0xFFFFFFFF;
    private const uint Grey = 0x808080FF;
    private const uint Black = 0x000000FF;

    [Fact]
    public void GreysTakeTheFirstColumnLightestFirst()
    {
        var columns = ColorPalette.Arrange([(1, Black), (2, Red), (3, White), (4, Grey)]);

        Assert.Equal([[3, 4, 1], [2]], columns);
    }

    [Fact]
    public void ColumnsFollowHueAndRunLightToDark()
    {
        var colors = new (ushort, uint)[] { (1, Magenta), (2, Blue), (3, Green), (4, Orange), (5, DarkRed), (6, PaleRed), (7, Red) };

        var columns = ColorPalette.Arrange(colors);

        // PaleRed (L 0.75), then the L 0.5 colours in hue order, then DarkRed (L 0.25)
        Assert.Equal([[6, 7, 4, 3, 2, 1, 5]], columns);
    }

    [Fact]
    public void HuesAreCutIntoFullColumnsInHueOrder()
    {
        // Ten reds then ten blues: exactly two full columns, reds first
        var reds = Enumerable.Range(0, ColorPalette.ColumnHeight).Select(i => ((ushort)(100 + i), (uint)(0x400000FF + (i << 26))));
        var blues = Enumerable.Range(0, ColorPalette.ColumnHeight).Select(i => ((ushort)(10 + i), (uint)(0x000040FF + (i << 10))));

        var columns = ColorPalette.Arrange([.. blues, .. reds]);

        Assert.Equal(2, columns.Count);
        Assert.All(columns[0], k => Assert.InRange(k, 100, 199));
        Assert.All(columns[1], k => Assert.InRange(k, 10, 99));
    }

    [Fact]
    public void ColumnsAreCutAtTheHeight()
    {
        var reds = Enumerable.Range(1, ColorPalette.ColumnHeight + 3)
            .Select(i => ((ushort)i, (uint)(0x200000FF + (i << 24))));

        var columns = ColorPalette.Arrange(reds);

        Assert.Equal([ColorPalette.ColumnHeight, 3], columns.Select(c => c.Count));
        // Same hue, so each column is just lightest first
        Assert.Equal(Enumerable.Range(1, ColorPalette.ColumnHeight).Reverse().Select(i => (ushort)i), columns[0]);
    }

    [Fact]
    public void HuesRunFromRedRoundToMagenta()
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        // Equally light, so one hue order; full and muted colours in each sixth of the circle, given out of order
        var inHueOrder = new uint[]
        {
            0xFF8000FF, 0xA08C5FFF, 0x8CA05FFF, 0x80FF00FF, 0x00FF80FF, 0x5FA08CFF,
            0x5F8CA0FF, 0x0080FFFF, 0x8000FFFF, 0x8C5FA0FF, 0xA05F8CFF, 0xFF0080FF,
        };
        var colors = inHueOrder.Select((rgba, i) => ((ushort)(i + 1), rgba)).Reverse();

        var columns = ColorPalette.Arrange(colors);

        Assert.Equal(Enumerable.Range(1, inHueOrder.Length).Select(i => (ushort)i), columns.SelectMany(c => c));
    }

    [Fact]
    public void NearWhiteReadsAsGrey()
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        Assert.Equal([[2], [1]], ColorPalette.Arrange([(1, Red), (2, 0xFFF0F0FF)]));
    }

    [Fact]
    public void MutedDarkColourIsNotGrey()
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        Assert.Equal([[1], [2]], ColorPalette.Arrange([(1, Grey), (2, 0x402626FF)]));
    }

    [Fact]
    public void PureGreyHasAHueLikeAnyGrey()
    {
        // revert-check: not revert-sensitive (kills a surviving mutant: pins behaviour the code already had)
        // Equally light, both hue 0, so the lower key comes first
        Assert.Equal([[1, 2]], ColorPalette.Arrange([(1, 0x817F7FFF), (2, Grey)]));
    }

    [Fact]
    public void DuplicatesKeepTheLowestKey()
    {
        Assert.Equal([[3]], ColorPalette.Arrange([(9, Red), (3, Red)]));
    }

    [Fact]
    public void TransparentAndNoColorAreLeftOut()
    {
        Assert.Empty(ColorPalette.Arrange([(ConfigurationBase.NoColor, Red), (5, 0xFF000000), (6, 0x00000000)]));
    }
}
