using SayMyName.Core;

namespace SayMyName.Core.Tests;

/// <summary>
/// Per-frame code that allocates makes the garbage collector pause the game. Each test here runs a
/// per-frame method like the game would and checks it allocates nothing; see "Per-frame code" in
/// CONTRIBUTING.md.
/// </summary>
public class PerFrameAllocationTests
{
    // The first runs allocate while the JIT compiles the code, so they don't count
    private const int WarmUpRuns = 3;
    private const int Frames = 100_000;

    [Fact]
    public void SaveDebouncerDoesNotAllocatePerFrame()
    {
        var debouncer = new SaveDebouncer(500);

        AssertNoAllocation(() => SimulateDragging(debouncer));
    }

    // Changes every 100 frames at 60 fps, so both the waiting and the saving branches run
    private static int SimulateDragging(SaveDebouncer debouncer)
    {
        var saves = 0;
        for (long frame = 0; frame < Frames; frame++)
        {
            var nowMs = frame * 16;
            if (frame % 100 == 0)
            {
                debouncer.Changed(nowMs);
            }

            if (debouncer.ShouldSave(nowMs))
            {
                saves++;
            }
        }

        return saves;
    }

    [Fact]
    public void TabPulseDoesNotAllocatePerFrame()
    {
        AssertNoAllocation(SimulatePulsingTab);
    }

    // As TabHighlighter's chat log PreDraw: the game's colour steps along its fade over 40 frames
    // (0.64 s at 60 fps), and every 7th frame is off the fade (another theme), so both of Fraction's
    // outcomes run
    private static int SimulatePulsingTab()
    {
        const uint ours = 0x3399FF;
        var onFade = 0;
        for (var frame = 0; frame < Frames; frame++)
        {
            var game = frame % 7 == 0 ? 0xFFFFFFu : TabPulse.Apply(TabPulse.Unread, frame % 40 / 40.0);
            var fraction = TabPulse.Fraction(game);
            if (fraction > 0 && TabPulse.Apply(ours, fraction) != ours)
            {
                onFade++;
            }
        }

        return onFade;
    }

    private static void AssertNoAllocation(Func<int> runFrames)
    {
        for (var i = 0; i < WarmUpRuns; i++)
        {
            runFrames();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = runFrames();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(result > 0, "the measured frames should reach the code under test");
        Assert.Equal(0, allocated);
    }
}
