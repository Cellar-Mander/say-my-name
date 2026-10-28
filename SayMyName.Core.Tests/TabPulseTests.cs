using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class TabPulseTests
{
    [Theory]
    // Colours the game set on an unread tab in one pulse, from /saymyname tabwatch
    [InlineData(0x8CFF5Au, 0.0)]
    [InlineData(0x8CFE5Au, 0.0)]
    [InlineData(0xA7DD7Fu, 0.5)]
    [InlineData(0xC2BCA4u, 1.0)]
    [InlineData(0x99EE6Cu, 0.25)]
    public void ReadsHowFarAlongTheGamesFadeItIs(uint game, double expected)
    {
        Assert.Equal(expected, TabPulse.Fraction(game), 0.05);
    }

    [Theory]
    // The normal tab colour, and others off the fade, don't pulse ours
    [InlineData(0xEEE1C5u)]
    [InlineData(0xFF7B1Au)]
    [InlineData(0x000000u)]
    public void ColoursOffTheFadeDontPulse(uint game)
    {
        Assert.Equal(0, TabPulse.Fraction(game));
    }

    [Fact]
    public void FadesOurColourAsFarTowardsTheFadedColour()
    {
        Assert.Equal(0xFF7B1Au, TabPulse.Apply(0xFF7B1A, 0));
        Assert.Equal(TabPulse.Faded, TabPulse.Apply(0xFF7B1A, 1));
        Assert.Equal(0xE09C5Fu, TabPulse.Apply(0xFF7B1A, 0.5));
    }

    [Fact]
    public void FollowsTheGamesPulseFrameByFrame()
    {
        // The game's own colour comes back unchanged, so ours fades exactly as the game's does
        foreach (var game in new uint[] { 0x8CFF5A, 0x9AED6D, 0xB1D18D, 0xC2BCA4 })
        {
            var ours = TabPulse.Apply(TabPulse.Unread, TabPulse.Fraction(game));
            Assert.InRange(Math.Abs((int)(ours >> 8 & 0xFF) - (int)(game >> 8 & 0xFF)), 0, 1);
        }
    }
}
