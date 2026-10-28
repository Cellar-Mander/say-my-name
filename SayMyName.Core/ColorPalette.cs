namespace SayMyName.Core;

/// <summary>Lays out the game's UIColor colours as a picker grid: one row per hue band, dark to light.</summary>
public static class ColorPalette
{
    /// <summary>Swatches per line; a band with more wraps onto further lines.</summary>
    public const int MaxPerLine = 16;

    // Twelve 30° bands, starting with red (centred on 0°)
    private const int HueBands = 12;

    // Below this saturation a colour reads as grey and goes in the first row
    private const double GreySaturation = 0.15;

    /// <summary>
    /// Arranges colours (UIColor rows as 0xRRGGBBAA) into lines of keys: greys first, then each hue band
    /// from red round to magenta, each sorted darkest first. Each colour appears once, under its lowest
    /// key; fully transparent colours and <see cref="ConfigurationBase.NoColor"/> are left out.
    /// </summary>
    public static List<List<ushort>> Arrange(IEnumerable<(ushort Key, uint Rgba)> colors)
    {
        var bands = new List<(ushort Key, double Lightness)>[HueBands + 1];
        var seen = new HashSet<uint>();
        foreach (var (key, rgba) in colors.OrderBy(c => c.Key))
        {
            if (key == ConfigurationBase.NoColor || (rgba & 0xFF) == 0 || !seen.Add(rgba))
            {
                continue;
            }

            var (band, lightness) = Classify(rgba);
            (bands[band] ??= []).Add((key, lightness));
        }

        var lines = new List<List<ushort>>();
        foreach (var band in bands)
        {
            if (band == null)
            {
                continue;
            }

            var sorted = band.OrderBy(c => c.Lightness).ThenBy(c => c.Key).Select(c => c.Key).ToList();
            for (var i = 0; i < sorted.Count; i += MaxPerLine)
            {
                lines.Add(sorted.GetRange(i, Math.Min(MaxPerLine, sorted.Count - i)));
            }
        }
        return lines;
    }

    // Band 0 is grey, 1..HueBands the hues; lightness is perceived brightness, 0 to 1
    private static (int Band, double Lightness) Classify(uint rgba)
    {
        var r = (rgba >> 24) / 255.0;
        var g = ((rgba >> 16) & 0xFF) / 255.0;
        var b = ((rgba >> 8) & 0xFF) / 255.0;
        var lightness = 0.299 * r + 0.587 * g + 0.114 * b;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        if (max == 0 || delta / max < GreySaturation)
        {
            return (0, lightness);
        }

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

        var degrees = 360.0 / HueBands;
        var band = (int)(((hue + degrees / 2) % 360 + 360) % 360 / degrees);
        return (1 + band, lightness);
    }
}
