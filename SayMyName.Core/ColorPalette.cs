namespace SayMyName.Core;

/// <summary>
/// Lays out the game's UIColor colours as a picker grid by HSL: columns run through the hues left
/// to right, and each column runs from light at the top to dark at the bottom.
/// </summary>
public static class ColorPalette
{
    /// <summary>Swatches per column. About 150 distinct colours make a grid about 15 wide.</summary>
    public const int ColumnHeight = 10;

    // Below this saturation, or this close to black or white, a colour reads as grey and goes in the first columns
    private const double GreySaturation = 0.12;
    private const double GreyLightnessMargin = 0.06;

    /// <summary>
    /// Arranges colours (UIColor rows as 0xRRGGBBAA) into columns of keys, each at most
    /// <see cref="ColumnHeight"/> long and lightest first: greys, then the rest in hue order from red,
    /// cut into columns. Every column but the last of the greys and the last of the hues is full.
    /// Each colour appears once, under its lowest key; fully transparent colours and
    /// <see cref="ConfigurationBase.NoColor"/> are left out.
    /// </summary>
    public static List<List<ushort>> Arrange(IEnumerable<(ushort Key, uint Rgba)> colors)
    {
        var greys = new List<(ushort Key, double Hue, double Lightness)>();
        var hues = new List<(ushort Key, double Hue, double Lightness)>();
        var seen = new HashSet<uint>();
        foreach (var (key, rgba) in colors.OrderBy(c => c.Key))
        {
            if (key == ConfigurationBase.NoColor || (rgba & 0xFF) == 0 || !seen.Add(rgba))
            {
                continue;
            }

            var (hue, saturation, lightness) = ToHsl(rgba);
            var grey = saturation < GreySaturation
                || lightness < GreyLightnessMargin || lightness > 1 - GreyLightnessMargin;
            (grey ? greys : hues).Add((key, hue, lightness));
        }

        var columns = new List<List<ushort>>();
        AddColumns(columns, greys);
        AddColumns(columns, [.. hues.OrderBy(c => c.Hue).ThenBy(c => c.Key)]);
        return columns;
    }

    private static void AddColumns(List<List<ushort>> columns, List<(ushort Key, double Hue, double Lightness)> colors)
    {
        for (var i = 0; i < colors.Count; i += ColumnHeight)
        {
            columns.Add([.. colors.GetRange(i, Math.Min(ColumnHeight, colors.Count - i))
                .OrderByDescending(c => c.Lightness).ThenBy(c => c.Hue).ThenBy(c => c.Key).Select(c => c.Key)]);
        }
    }

    // Hue in degrees [0, 360), saturation and lightness 0 to 1
    private static (double Hue, double Saturation, double Lightness) ToHsl(uint rgba)
    {
        var r = (rgba >> 24) / 255.0;
        var g = ((rgba >> 16) & 0xFF) / 255.0;
        var b = ((rgba >> 8) & 0xFF) / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2;
        if (delta == 0)
        {
            return (0, 0, lightness);
        }

        var saturation = delta / (1 - Math.Abs(2 * lightness - 1));
        double hue;
        if (max == r)
        {
            hue = 60 * ((g - b) / delta);
        }
        else if (max == g)
        {
            hue = 60 * ((b - r) / delta + 2);
        }
        else
        {
            hue = 60 * ((r - g) / delta + 4);
        }
        return ((hue + 360) % 360, saturation, lightness);
    }
}
