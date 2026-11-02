using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;

namespace Core.Host.Tests;

/// <summary>
/// The delay of a refused entry (#71, decision 0048), with a clock that does not sleep: the curve to its exact ceiling, the two counters, the zeroing, the origin kept as a prefix,
/// the limit of answers that wait and the same delay for a name nobody goes by, which the delay cannot tell from a wrong password because it is only ever given the name typed.
/// </summary>
public sealed class SignInDelayTests
{
    private const int TestDeadlineMilliseconds = 15_000;
    private static readonly IPAddress Here = IPAddress.Parse("203.0.113.7");
    private static readonly int[] CurveSeconds = [0, 1, 2, 4, 8, 16, 30, 30, 30];
    private static readonly int[] Refusals = [0, 1, 2, 3, 4, 5, 6, 7, 8, 100, int.MaxValue];
    private static readonly int[] WaitedSeconds = [0, 0, 1, 2, 4, 8, 16, 30, 30, 30, 30];

    [Fact]
    public async Task RefusalsInARowWaitNothingThenOneSecondDoublingToTheCeilingOfThirtySeconds()
    {
        var (clock, delay) = Made();

        var waited = new List<TimeSpan>();
        for (var refusal = 0; refusal < CurveSeconds.Length; refusal++)
        {
            waited.Add(await RefusedAsync(clock, delay, "ana", Here));
        }

        Assert.Equal(CurveSeconds.Select(seconds => TimeSpan.FromSeconds(seconds)), waited);
    }

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public async Task ADelayDoesNotEndBeforeItsTimeHasPassed()
    {
        var (clock, delay) = Made();
        _ = await RefusedAsync(clock, delay, "ana", Here);

        var second = delay.RefusedAsync("ana", Here, TestContext.Current.CancellationToken);
        clock.Advance(SignInDelay.First - TimeSpan.FromTicks(1));
        Assert.False(second.IsCompleted);
        clock.Advance(TimeSpan.FromTicks(1));
        await second;

        Assert.True(second.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task TwoAddressesOfTheSameNetworkPrefixAddUpAndAnotherPrefixDoesNot()
    {
        var (clock, delay) = Made();
        _ = await RefusedAsync(clock, delay, "ana", IPAddress.Parse("198.51.100.1"));

        var sameNetwork = await RefusedAsync(clock, delay, "bia", IPAddress.Parse("198.51.100.200"));
        var otherNetwork = await RefusedAsync(clock, delay, "caio", IPAddress.Parse("198.51.101.1"));

        Assert.Equal((SignInDelay.First, TimeSpan.Zero), (sameNetwork, otherNetwork));
    }

    [Fact]
    public async Task TheGreaterOfTheNameAndTheOriginCounterDecides()
    {
        var (clock, delay) = Made();
        _ = await RefusedAsync(clock, delay, "ana", IPAddress.Parse("192.0.2.1"));
        _ = await RefusedAsync(clock, delay, "ana", IPAddress.Parse("192.0.2.2"));

        var fromElsewhere = await RefusedAsync(clock, delay, "ana", IPAddress.Parse("100.64.0.1"));

        Assert.Equal(TimeSpan.FromSeconds(2), fromElsewhere);
    }

    [Fact]
    public async Task AnAcceptedEntryZeroesTheNameAndTheOrigin()
    {
        var (clock, delay) = Made();
        for (var refusal = 0; refusal < 4; refusal++)
        {
            _ = await RefusedAsync(clock, delay, "ana", Here);
        }

        delay.Accepted("ana", Here);

        Assert.Equal(TimeSpan.Zero, await RefusedAsync(clock, delay, "bia", Here));
        Assert.Equal(TimeSpan.Zero, await RefusedAsync(clock, delay, "ana", IPAddress.Parse("100.64.0.9")));
    }

    [Fact]
    public async Task FifteenMinutesWithoutARefusalZeroTheCountersAndOneSecondLessDoesNot()
    {
        var (clock, delay) = Made();
        for (var refusal = 0; refusal < 3; refusal++)
        {
            _ = await RefusedAsync(clock, delay, "ana", Here);
        }

        var alreadyWaited = SignInDelay.DelayOf(3);
        clock.Advance(SignInDelay.Forgetting - alreadyWaited - TimeSpan.FromSeconds(1));
        var before = await RefusedAsync(clock, delay, "ana", Here);
        clock.Advance(SignInDelay.Forgetting);
        var after = await RefusedAsync(clock, delay, "ana", Here);

        Assert.Equal((TimeSpan.FromSeconds(4), TimeSpan.Zero), (before, after));
    }

    [Fact]
    public async Task TheCountersOfWhatPassedTheWindowAreForgottenSoMemoryDoesNotGrow()
    {
        var (clock, delay) = Made();
        for (var index = 0; index < 50; index++)
        {
            _ = await RefusedAsync(clock, delay, $"nome-{index}", IPAddress.Parse($"10.1.{index}.1"));
        }

        clock.Advance(SignInDelay.Forgetting + TimeSpan.FromMinutes(2));
        _ = await RefusedAsync(clock, delay, "ana", Here);

        Assert.Equal((1, 1), (KeysIn(delay, "typed").Count, KeysIn(delay, "origins").Count));
    }

    [Fact]
    public async Task WithTheTablesFullAKnownNameAndOriginKeepCountingAndANewOneOfEachGetsNoCounterAndNoDelay()
    {
        var (clock, delay) = Made();
        for (var index = 0; index < SignInDelay.MaxCounters; index++)
        {
            Assert.Equal(TimeSpan.Zero, await RefusedAsync(clock, delay, $"nome-{index}", NetworkNumber(index)));
        }

        Assert.Equal((SignInDelay.MaxCounters, SignInDelay.MaxCounters), (KeysIn(delay, "typed").Count, KeysIn(delay, "origins").Count));

        var knownName = await RefusedAsync(clock, delay, "nome-0", NetworkNumber(SignInDelay.MaxCounters));
        var knownOrigin = await RefusedAsync(clock, delay, "nome-novo-a", NetworkNumber(1));
        var newNameInANewOriginTwice = (
            await RefusedAsync(clock, delay, "nome-novo-b", NetworkNumber(SignInDelay.MaxCounters + 1)),
            await RefusedAsync(clock, delay, "nome-novo-b", NetworkNumber(SignInDelay.MaxCounters + 1)));

        Assert.Equal((SignInDelay.First, SignInDelay.First, (TimeSpan.Zero, TimeSpan.Zero)), (knownName, knownOrigin, newNameInANewOriginTwice));
        Assert.Equal((SignInDelay.MaxCounters, SignInDelay.MaxCounters), (KeysIn(delay, "typed").Count, KeysIn(delay, "origins").Count));
    }

    [Fact(Timeout = TestDeadlineMilliseconds)]
    public async Task ACancelledWaitFreesItsPlaceAmongTheAnswersThatWait()
    {
        var (clock, delay) = Made();
        _ = await RefusedAsync(clock, delay, "ana", Here);
        using var cancel = new CancellationTokenSource();
        var cancelled = delay.RefusedAsync("ana", Here, cancel.Token);

        await cancel.CancelAsync();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal(0, clock.Waiting);
        var next = delay.RefusedAsync("ana", Here, TestContext.Current.CancellationToken);
        Assert.Equal(1, clock.Waiting);
        clock.Advance(SignInDelay.Ceiling);
        await next;
    }

    [Theory]
    [InlineData("10.20.30.40", "v4:0A141E")]
    [InlineData("::ffff:10.20.30.40", "v4:0A141E")]
    [InlineData("2001:db8:abcd:1234:5678:9abc:def0:1234", "v6:20010DB8ABCD")]
    public void OriginIsKeptAsThePrefixOfTheNetworkAndNeverAsTheWholeAddress(string address, string expected) =>
        Assert.Equal(expected, SignInDelay.OriginKey(IPAddress.Parse(address)));

    [Fact]
    public void AnEntryWithNoAddressIsCountedTogether() => Assert.Equal(SignInDelay.OriginKey(null), SignInDelay.OriginKey(null));

    [Fact]
    public async Task WhatWasTypedIsNeverKeptAndTheWholeAddressNeither()
    {
        var (clock, delay) = Made();
        _ = await RefusedAsync(clock, delay, "ana-do-teste", IPAddress.Parse("203.0.113.77"));

        var names = KeysIn(delay, "typed");
        var origins = KeysIn(delay, "origins");

        Assert.Equal(["v4:CB0071"], origins);
        var kept = Assert.Single(names);
        Assert.Equal(64, kept.Length);
        Assert.DoesNotContain("ana", kept, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheCurveIsOneSecondDoublingUpToTheCeilingWhateverTheNumberOfRefusals()
    {
        var delays = Refusals.Select(SignInDelay.DelayOf);

        Assert.Equal(WaitedSeconds.Select(seconds => TimeSpan.FromSeconds(seconds)), delays);
    }

    [Fact]
    [SuppressMessage("Security", "CA5394", Justification = "The seeded sequence is a test input, never a secret.")]
    public async Task AnySequenceOfRefusalsAcceptancesAndPausesGivesTheDelayOfTheModel()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var random = new Random(seed);
            var (clock, delay) = Made();
            var model = new ReferenceModel(clock);
            for (var step = 0; step < 120; step++)
            {
                var name = $"nome-{random.Next(3)}";
                var origin = IPAddress.Parse($"192.0.{random.Next(3)}.{random.Next(1, 250)}");
                switch (random.Next(8))
                {
                    case 0:
                        delay.Accepted(name, origin);
                        model.Accepted(name, origin);
                        break;
                    case 1:
                        clock.Advance(TimeSpan.FromSeconds(random.Next(0, 1200)));
                        break;
                    default:
                        var expected = model.Refused(name, origin);
                        Assert.Equal(expected, await RefusedAsync(clock, delay, name, origin));
                        break;
                }
            }
        }
    }

    /// <summary>The address 10.x.y.1 of the network that number stands for, one /24 apart from every other number.</summary>
    internal static IPAddress NetworkNumber(int number) => new([10, (byte)(number >> 8), (byte)(number & 0xFF), 1]);

    private static (ManualClock Clock, SignInDelay Delay) Made()
    {
        var clock = new ManualClock();
        return (clock, new SignInDelay(clock));
    }

    /// <summary>Refuses once and returns how long the answer waited, moving the clock by exactly that.</summary>
    private static async Task<TimeSpan> RefusedAsync(ManualClock clock, SignInDelay delay, string name, IPAddress origin)
    {
        var answer = delay.RefusedAsync(name, origin, TestContext.Current.CancellationToken);
        if (answer.IsCompleted)
        {
            await answer;
            return TimeSpan.Zero;
        }

        var waited = clock.NextDue!.Value;
        clock.Advance(waited);
        await answer;
        return waited;
    }

    private static List<string> KeysIn(SignInDelay delay, string field)
    {
        var counters = (IDictionary)typeof(SignInDelay).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(delay)!;
        return [.. counters.Keys.Cast<string>()];
    }

    /// <summary>The rule of the decision written again in the plainest way, to compare against.</summary>
    private sealed class ReferenceModel(ManualClock clock)
    {
        private readonly Dictionary<string, (int Count, DateTimeOffset Last)> names = [];
        private readonly Dictionary<string, (int Count, DateTimeOffset Last)> origins = [];

        public void Accepted(string name, IPAddress origin)
        {
            _ = names.Remove(name);
            _ = origins.Remove(SignInDelay.OriginKey(origin));
        }

        public TimeSpan Refused(string name, IPAddress origin)
        {
            var count = Math.Max(Bump(names, name), Bump(origins, SignInDelay.OriginKey(origin)));
            return count < 2 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Min(Math.Pow(2, count - 2), 30));
        }

        private int Bump(Dictionary<string, (int Count, DateTimeOffset Last)> counters, string key)
        {
            var now = clock.GetUtcNow();
            var count = counters.TryGetValue(key, out var seen) && now - seen.Last < TimeSpan.FromMinutes(15) ? seen.Count + 1 : 1;
            counters[key] = (count, now);
            return count;
        }
    }
}
