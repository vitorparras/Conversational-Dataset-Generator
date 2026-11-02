using System.Diagnostics;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The entry does not tell a name that exists from one that does not by the time it takes: a name nobody goes by is checked against nobody, which derives the password
/// all the same. Each refusal is timed as the first of its series, because a series from one address earns a delay (0048).
/// The property is tested coarsely, with a work factor high enough for the derivation to be most of the time of an entry; the sameness of the answer is in SessionRoutesTests.
/// </summary>
[Trait("Category", "Database")]
public sealed class SessionEntryTimingTests
{
    private const int HeavyFactor = 600_000;
    private const int Attempts = 3;

    [DatabaseFact]
    public async Task NameNobodyGoesByTakesAboutAsLongAsAWrongPasswordBecauseTheDerivationIsMadeAllTheSame()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", HeavyFactor, cancellationToken);
        _ = await TimeAsync(scenario, () => scenario.Name, cancellationToken);

        var wrong = await FastestAsync(scenario, () => scenario.Name, cancellationToken);
        var unknown = await FastestAsync(scenario, () => $"zqx-ninguem-{Guid.NewGuid():N}", cancellationToken);

        Assert.True(wrong > TimeSpan.FromMilliseconds(20), $"The derivation is not most of the time of an entry ({wrong}); the property cannot be judged.");
        Assert.True(unknown > wrong / 2, $"A name nobody goes by took {unknown}, a wrong password {wrong}.");
    }

    private static async Task<TimeSpan> FastestAsync(SessionScenario scenario, Func<string> name, CancellationToken cancellationToken)
    {
        var times = new List<TimeSpan>();
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            times.Add(await TimeAsync(scenario, name, cancellationToken));
        }

        return times.Min();
    }

    /// <summary>
    /// One refusal timed on its own: an accepted entry before it zeroes the counters of the origin and of the name, so the refusal is the first of its series and
    /// waits no delay of its own (decision 0048); what is measured is the derivation, not the delay that repeated refusals from this one address would earn.
    /// </summary>
    private static async Task<TimeSpan> TimeAsync(SessionScenario scenario, Func<string> name, CancellationToken cancellationToken)
    {
        using var accepted = await scenario.EnterAsync(scenario.Name, scenario.Password, cancellationToken);
        Assert.True(accepted.IsSuccessStatusCode, $"The entry with the right password was refused ({accepted.StatusCode}).");
        var clock = Stopwatch.StartNew();
        using var response = await scenario.EnterAsync(name(), "zqx-errada", cancellationToken);
        return clock.Elapsed;
    }
}
