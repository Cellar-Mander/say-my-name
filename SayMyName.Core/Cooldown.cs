namespace SayMyName.Core;

/// <summary>Lets an alert fire at most once per cooldown, so a busy chat doesn't spam sounds.</summary>
public sealed class Cooldown
{
    private long? lastFiredMs;

    /// <summary>Fires and restarts the cooldown if it has run out; otherwise does nothing.</summary>
    /// <param name="nowMs">A monotonic clock in milliseconds, e.g. <see cref="Environment.TickCount64"/>.</param>
    public bool TryFire(long nowMs, TimeSpan length)
    {
        if (!IsReady(nowMs, length))
        {
            return false;
        }

        lastFiredMs = nowMs;
        return true;
    }

    /// <summary>Whether <see cref="TryFire"/> would fire now, without restarting the cooldown.</summary>
    public bool IsReady(long nowMs, TimeSpan length) =>
        lastFiredMs is not { } last || nowMs - last >= length.TotalMilliseconds;
}
