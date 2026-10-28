namespace SayMyName.Core;

/// <summary>
/// Pulses a coloured chat tab's name in step with the game's own unread-tab pulse. Colours are 0xRRGGBB.
/// </summary>
/// <remarks>
/// Measured in game (default theme): an unread tab's name holds <see cref="Unread"/>, fades in a
/// straight line to <see cref="Faded"/> over about half a second, then snaps back, about every 0.64 s.
/// The game sets the colour from its own clock each frame, so reading how far along the fade it is
/// and fading our colour just as far towards <see cref="Faded"/> keeps both in step.
/// </remarks>
public static class TabPulse
{
    public const uint Unread = 0x8CFF5A;
    public const uint Faded = 0xC2BCA4;

    // How far off the fade a colour may lie and still count as on it, per channel, for rounding
    private const int Tolerance = 6;

    /// <summary>
    /// How far along its fade the game's colour for a tab is, from 0 (<see cref="Unread"/>) to 1
    /// (<see cref="Faded"/>), or 0 when it isn't on the fade (another theme, or not pulsing).
    /// </summary>
    public static double Fraction(uint game)
    {
        var (dr, dg, db) = Diff(Faded, Unread);
        var (cr, cg, cb) = Diff(game, Unread);
        var length2 = (double)(dr * dr + dg * dg + db * db);
        var t = Math.Clamp((cr * dr + cg * dg + cb * db) / length2, 0, 1);
        var (er, eg, eb) = (cr - t * dr, cg - t * dg, cb - t * db);
        return er * er + eg * eg + eb * eb <= 3 * Tolerance * Tolerance ? t : 0;
    }

    /// <summary>Our colour <paramref name="fraction"/> of the way along its fade.</summary>
    public static uint Apply(uint color, double fraction)
    {
        var r = Lerp(color >> 16, Faded >> 16, fraction);
        var g = Lerp(color >> 8, Faded >> 8, fraction);
        var b = Lerp(color, Faded, fraction);
        return r << 16 | g << 8 | b;
    }

    private static (int R, int G, int B) Diff(uint a, uint b) =>
        ((int)(a >> 16 & 0xFF) - (int)(b >> 16 & 0xFF), (int)(a >> 8 & 0xFF) - (int)(b >> 8 & 0xFF), (int)(a & 0xFF) - (int)(b & 0xFF));

    private static uint Lerp(uint from, uint to, double t)
    {
        var (f, d) = (from & 0xFF, to & 0xFF);
        return (uint)Math.Round(f + (d - (double)f) * t);
    }
}
