using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class SaveDebouncerTests
{
    private const long Delay = 500;

    [Fact]
    public void NoChangeNeverSaves()
    {
        var debouncer = new SaveDebouncer(Delay);

        Assert.False(debouncer.ShouldSave(10_000));
        Assert.False(debouncer.Flush());
    }

    [Fact]
    public void SavesOnceTheDelayHasPassed()
    {
        var debouncer = new SaveDebouncer(Delay);
        debouncer.Changed(1_000);

        Assert.False(debouncer.ShouldSave(1_000 + Delay - 1));
        Assert.True(debouncer.ShouldSave(1_000 + Delay));
        Assert.False(debouncer.ShouldSave(1_000 + Delay + 1));
    }

    [Fact]
    public void EachChangeRestartsTheWait()
    {
        var debouncer = new SaveDebouncer(Delay);
        debouncer.Changed(1_000);
        debouncer.Changed(1_300);

        Assert.False(debouncer.ShouldSave(1_000 + Delay));
        Assert.True(debouncer.ShouldSave(1_300 + Delay));
    }

    [Fact]
    public void FlushReportsAnUnsavedChangeOnce()
    {
        var debouncer = new SaveDebouncer(Delay);
        debouncer.Changed(1_000);

        Assert.True(debouncer.Flush());
        Assert.False(debouncer.Flush());
        Assert.False(debouncer.ShouldSave(1_000 + Delay));
    }

    [Fact]
    public void FlushAfterASaveHasNothingLeft()
    {
        var debouncer = new SaveDebouncer(Delay);
        debouncer.Changed(1_000);
        debouncer.ShouldSave(1_000 + Delay);

        Assert.False(debouncer.Flush());
    }

    [Fact]
    public void NegativeDelayIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SaveDebouncer(-1));
    }
}
