using System.Diagnostics;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Sessions;

/// <summary>
/// The entry does not tell a name that exists from one that does not by the time it takes: a name nobody goes by is checked against nobody, which derives the password
/// all the same. Each refusal is timed as the first of its series, because a series from one address earns a delay (0048).
/// The property is tested coarsely, with a work factor high enough for the derivation to be most of the time of an entry; the sameness of the answer is in SessionRoutesTests.
/// The two entries are timed side by side, one pair after the other with the order alternating, so that both of a pair meet the same load: timed in two blocks, a load that
/// changes between them (other test projects starting and ending on the same cores) moves one side by more than the half the property allows, with the property unchanged.
/// Load only adds time, so the fastest entry of each side is the best reading of what it costs; but each side needs a quiet moment to show it, and on a busy machine one side may not
/// get one in five pairs while the other does (#566: a name nobody goes by at 0.20 s and a wrong password at 0.53 s and at 0.72 s, in two runs). The pairs go on, up to a limit, until the fastest
/// of each side agree within the same margin; a derivation skipped never agrees, because the entry that skips it costs milliseconds against the hundreds of the one that derives.
/// </summary>
[Trait("Category", "Database")]
public sealed class SessionEntryTimingTests
{
    private const int HeavyFactor = 600_000;
    private const int MinPairs = 5;
    private const int MaxPairs = 20;
    private static readonly TimeSpan Floor = TimeSpan.FromMilliseconds(20);

    [DatabaseFact]
    public async Task NameNobodyGoesByTakesAboutAsLongAsAWrongPasswordBecauseTheDerivationIsMadeAllTheSame()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scenario = await SessionScenario.StartAsync("00:30:00", HeavyFactor, cancellationToken);
        string WrongName() => scenario.Name;
        _ = await TimeAsync(scenario, WrongName, cancellationToken);
        _ = await TimeAsync(scenario, UnknownName, cancellationToken);

        var (wrong, unknown) = await FastestPairsAsync(scenario, WrongName, cancellationToken);

        Assert.True(wrong > Floor, $"The derivation is not most of the time of an entry ({wrong}); the property cannot be judged.");
        Assert.True(unknown > wrong / 2, $"A name nobody goes by took {unknown}, a wrong password {wrong}, after {MaxPairs} pairs.");
    }

    private static bool Indistinguishable(TimeSpan wrong, TimeSpan unknown) => wrong > Floor && unknown > wrong / 2;

    private static string UnknownName() => $"zqx-ninguem-{Guid.NewGuid():N}";

    private static async Task<(TimeSpan Wrong, TimeSpan Unknown)> FastestPairsAsync(SessionScenario scenario, Func<string> wrongName, CancellationToken cancellationToken)
    {
        var wrong = TimeSpan.MaxValue;
        var unknown = TimeSpan.MaxValue;
        for (var pair = 0; pair < MaxPairs && !(pair >= MinPairs && Indistinguishable(wrong, unknown)); pair++)
        {
            var unknownFirst = (pair & 1) == 1;
            var first = await TimeAsync(scenario, unknownFirst ? UnknownName : wrongName, cancellationToken);
            var second = await TimeAsync(scenario, unknownFirst ? wrongName : UnknownName, cancellationToken);
            wrong = TimeSpan.FromTicks(Math.Min(wrong.Ticks, (unknownFirst ? second : first).Ticks));
            unknown = TimeSpan.FromTicks(Math.Min(unknown.Ticks, (unknownFirst ? first : second).Ticks));
        }

        return (wrong, unknown);
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
