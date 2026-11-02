using System.Diagnostics;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The entry does not tell a name that exists from one that does not by the time it takes: a name nobody goes by is checked against nobody, which derives the password
/// all the same. The property is tested coarsely, with a work factor high enough for the derivation to be most of the time of an entry; the sameness of the answer is in SessionRoutesTests.
/// The two entries are timed side by side, one pair after the other with the order alternating, so that both of a pair meet the same load: timed in two blocks, a load that
/// changes between them (other test projects starting and ending on the same cores) moves one side by more than the half the property allows, with the property unchanged.
/// </summary>
[Trait("Category", "Database")]
public sealed class SessionEntryTimingTests
{
    private const int HeavyFactor = 600_000;
    private const int Pairs = 5;

    [DatabaseFact]
    public async Task NameNobodyGoesByTakesAboutAsLongAsAWrongPasswordBecauseTheDerivationIsMadeAllTheSame()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", HeavyFactor, cancellationToken);
        _ = await TimeAsync(scenario, scenario.Name, "zqx-errada", cancellationToken);
        _ = await TimeAsync(scenario, "zqx-ninguem", "zqx-errada", cancellationToken);

        var (wrong, unknown) = await FastestPairsAsync(scenario, cancellationToken);

        Assert.True(wrong > TimeSpan.FromMilliseconds(20), $"The derivation is not most of the time of an entry ({wrong}); the property cannot be judged.");
        Assert.True(unknown > wrong / 2, $"A name nobody goes by took {unknown}, a wrong password {wrong}.");
    }

    private static async Task<(TimeSpan Wrong, TimeSpan Unknown)> FastestPairsAsync(SessionScenario scenario, CancellationToken cancellationToken)
    {
        var wrong = TimeSpan.MaxValue;
        var unknown = TimeSpan.MaxValue;
        for (var pair = 0; pair < Pairs; pair++)
        {
            var unknownFirst = (pair & 1) == 1;
            var first = await TimeAsync(scenario, unknownFirst ? "zqx-ninguem" : scenario.Name, "zqx-errada", cancellationToken);
            var second = await TimeAsync(scenario, unknownFirst ? scenario.Name : "zqx-ninguem", "zqx-errada", cancellationToken);
            wrong = TimeSpan.FromTicks(Math.Min(wrong.Ticks, (unknownFirst ? second : first).Ticks));
            unknown = TimeSpan.FromTicks(Math.Min(unknown.Ticks, (unknownFirst ? first : second).Ticks));
        }

        return (wrong, unknown);
    }

    private static async Task<TimeSpan> TimeAsync(SessionScenario scenario, string name, string password, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        using var response = await scenario.EnterAsync(name, password, cancellationToken);
        return clock.Elapsed;
    }
}
