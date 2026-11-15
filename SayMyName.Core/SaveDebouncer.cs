namespace SayMyName.Core;

/// <summary>
/// Decides when to save settings that can change on every frame, such as while a slider is dragged,
/// so the disk write happens once the value settles instead of on each frame.
/// </summary>
/// <remarks>
/// <see cref="ShouldSave"/> runs every frame, so it must not allocate; see "Per-frame code" in
/// CONTRIBUTING.md.
/// </remarks>
public sealed class SaveDebouncer
{
    private readonly long delayMs;
    private long dueAtMs;
    private bool pending;

    public SaveDebouncer(long delayMs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(delayMs);
        this.delayMs = delayMs;
    }

    /// <summary>Call when a setting changes; restarts the wait.</summary>
    public void Changed(long nowMs)
    {
        pending = true;
        dueAtMs = nowMs + delayMs;
    }

    /// <summary>Call every frame; true once, on the first call at least the delay after the last change.</summary>
    public bool ShouldSave(long nowMs)
    {
        if (!pending || nowMs < dueAtMs)
        {
            return false;
        }

        pending = false;
        return true;
    }

    /// <summary>True if a change has not been saved yet, and forgets it; call on close or unload.</summary>
    public bool Flush()
    {
        var wasPending = pending;
        pending = false;
        return wasPending;
    }
}
