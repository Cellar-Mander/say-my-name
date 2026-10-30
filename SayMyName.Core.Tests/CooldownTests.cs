using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class CooldownTests
{
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    [Fact]
    public void FirstAlertFires()
    {
        Assert.True(new Cooldown().TryFire(0, FiveSeconds));
    }

    [Fact]
    public void AlertsWithinTheCooldownAreSuppressed()
    {
        var cooldown = new Cooldown();
        cooldown.TryFire(1_000, FiveSeconds);

        Assert.False(cooldown.TryFire(1_000, FiveSeconds));
        Assert.False(cooldown.TryFire(5_999, FiveSeconds));
        Assert.True(cooldown.TryFire(6_000, FiveSeconds));
    }

    [Fact]
    public void SuppressedAlertsDoNotExtendTheCooldown()
    {
        var cooldown = new Cooldown();
        cooldown.TryFire(0, FiveSeconds);
        cooldown.TryFire(4_000, FiveSeconds);

        Assert.True(cooldown.TryFire(5_000, FiveSeconds));
    }

    [Fact]
    public void ZeroCooldownAlwaysFires()
    {
        var cooldown = new Cooldown();
        Assert.True(cooldown.TryFire(0, TimeSpan.Zero));
        Assert.True(cooldown.TryFire(0, TimeSpan.Zero));
    }

    [Fact]
    public void IsReadyReportsWithoutRestartingTheCooldown()
    {
        var cooldown = new Cooldown();
        Assert.True(cooldown.IsReady(0, FiveSeconds));
        Assert.True(cooldown.IsReady(0, FiveSeconds));

        cooldown.TryFire(1_000, FiveSeconds);
        Assert.False(cooldown.IsReady(5_999, FiveSeconds));
        Assert.True(cooldown.IsReady(6_000, FiveSeconds));
        Assert.True(cooldown.TryFire(6_000, FiveSeconds));
    }
}
