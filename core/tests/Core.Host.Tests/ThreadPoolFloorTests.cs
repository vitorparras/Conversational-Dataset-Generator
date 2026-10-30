namespace Core.Host.Tests;

/// <summary>The pool of the test process makes threads at once up to a floor high enough for the blocking waits of the suite.</summary>
public sealed class ThreadPoolFloorTests
{
    [Fact]
    public void FloorIsTheOneTheModuleSets()
    {
        ThreadPool.GetMinThreads(out var workers, out var completions);

        Assert.Equal(ThreadPoolFloor.Threads, workers);
        Assert.Equal(ThreadPoolFloor.Threads, completions);
    }
}
