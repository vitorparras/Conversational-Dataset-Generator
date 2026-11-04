namespace Core.Host.Tests;

/// <summary>The state that keeps the warning of the seal for /status (issue #62, part B2b): it starts with none, takes the warning whole and clears it, and readers see one whole warning while writers replace it.</summary>
public sealed class SealWarningStateTests
{
    [Fact]
    public void StartsWithNoWarning() => Assert.Null(new SealWarningState().Current);

    [Fact]
    public void TakesTheWarningAndClearsIt()
    {
        var state = new SealWarningState();

        state.Record("um");
        var taken = state.Current;
        state.Record(null);

        Assert.Equal(("um", null), (taken, state.Current));
    }

    [Fact]
    public async Task ReadersSeeOnlyWholeWarningsWhileWritersReplaceThem()
    {
        var state = new SealWarningState();
        var seen = new System.Collections.Concurrent.ConcurrentBag<string?>();
        var writers = Enumerable.Range(0, 4).Select(index => Task.Run(
            () =>
            {
                for (var round = 0; round < 500; round++)
                {
                    state.Record(index % 2 == 0 ? "aviso A" : null);
                    seen.Add(state.Current);
                }
            },
            TestContext.Current.CancellationToken));

        await Task.WhenAll(writers);

        Assert.All(seen, warning => Assert.True(warning is null or "aviso A"));
    }
}
