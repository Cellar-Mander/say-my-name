using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class ColorPaletteTests
{
    private const uint Red = 0xFF0000FF;
    private const uint DarkRed = 0x800000FF;
    private const uint Green = 0x00FF00FF;
    private const uint Blue = 0x0000FFFF;
    private const uint Magenta = 0xFF00FFFF;
    private const uint Orange = 0xFF8000FF;
    private const uint White = 0xFFFFFFFF;
    private const uint Black = 0x000000FF;

    [Fact]
    public void GreysComeFirstThenHuesRoundTheWheel()
    {
        var lines = ColorPalette.Arrange([(1, Magenta), (2, Blue), (3, White), (4, Green), (5, Orange), (6, Red), (7, Black)]);

        Assert.Equal([[7, 3], [6], [5], [4], [2], [1]], lines);
    }

    [Fact]
    public void EachBandRunsDarkToLight()
    {
        Assert.Equal([[2, 1]], ColorPalette.Arrange([(1, Red), (2, DarkRed)]));
    }

    [Fact]
    public void RedJustBelow360GoesWithRed()
    {
        // Hue about 355°
        Assert.Single(ColorPalette.Arrange([(1, Red), (2, 0xFF0015FF)]));
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

    [Fact]
    public void LongBandsWrap()
    {
        var reds = Enumerable.Range(1, ColorPalette.MaxPerLine + 3)
            .Select(i => ((ushort)i, (uint)(0x400000FF + (i << 24))));

        var lines = ColorPalette.Arrange(reds);

        Assert.Equal([ColorPalette.MaxPerLine, 3], lines.Select(l => l.Count));
        Assert.Equal(Enumerable.Range(1, ColorPalette.MaxPerLine + 3).Select(i => (ushort)i), lines.SelectMany(l => l));
    }
}
