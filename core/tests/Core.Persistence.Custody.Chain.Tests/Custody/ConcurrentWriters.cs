using System.Diagnostics;
using System.Security.Cryptography;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;

namespace Core.Persistence.Tests.Custody;

/// <summary>The people the planned authors stand for.</summary>
/// <param name="Active">A person who is active.</param>
/// <param name="Deactivated">A person who was deactivated.</param>
internal sealed record PlannedPeople(long Active, long Deactivated);

/// <summary>What one planned append was given as its answer, and the item it was about.</summary>
internal sealed record Answered(PlannedAppend Plan, long Item, CustodyResult Result);

/// <summary>What a case did: the event it began with, the first item of its own and what each writer was answered.</summary>
internal sealed record CaseRun(long FirstItem, long Seed, IReadOnlyList<Answered> Answers);

/// <summary>
/// Runs the writers of a plan against the chain at once. A writer told the chain is busy asks again until it is let in, as whoever operates would,
/// so that the answers a case ends with are the ones the chain gives when it settles. The appends in flight are limited to what the server's connections allow.
/// </summary>
internal sealed class ConcurrentWriters
{
    /// <summary>The appends in flight at once, which is also the capacity of the pool the writers go through.</summary>
    internal const int MostInFlight = 12;

    private const int ItemsPerCase = 100;
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private readonly CustodyChain chain;
    private readonly PlannedPeople people;
    private static readonly SemaphoreSlim InFlight = new(MostInFlight);

    private long nextItem = 20_000_000;

    internal ConcurrentWriters(CustodyChain chain, PlannedPeople people)
    {
        this.chain = chain;
        this.people = people;
    }

    /// <summary>The writers of the chain through a pool of the capacity they may have in flight, so that no append opens a connection of its own.</summary>
    internal static async Task<ConcurrentWriters> PooledAsync(CustodyDatabase database, PlannedPeople people) =>
        new(await database.PooledChainAsync(MostInFlight), people);

    internal static bool Owns(CaseRun run, long item) => item >= run.FirstItem && item < run.FirstItem + ItemsPerCase;

    internal CaseRun Run(WriterPlan plan)
    {
        var first = Interlocked.Add(ref nextItem, ItemsPerCase) - ItemsPerCase;
        var seed = SettleAsync(CustodyEvent.Of(CustodyAuthor.System, Item(first), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct)).GetAwaiter().GetResult();
        var writers = plan.Writers
            .Select((appends, number) => Task.Run(() => PerformAsync(appends, first + 1 + (number * WriterPlan.MostAppendsPerWriter), seed.Sequence!.Value)))
            .ToArray();
        var answers = Task.WhenAll(writers).GetAwaiter().GetResult().SelectMany(done => done).ToList();
        return new CaseRun(first, seed.Sequence!.Value, answers);
    }

    private static CustodyItem Item(long id) => new(CustodyItemKind.Person, id);

    private async Task<List<Answered>> PerformAsync(IEnumerable<PlannedAppend> appends, long firstItem, long seed)
    {
        var done = new List<Answered>();
        foreach (var (planned, index) in appends.Select((planned, index) => (planned, index)))
        {
            var item = firstItem + index;
            done.Add(new Answered(planned, item, await SettleAsync(EventFor(planned, item, seed)).ConfigureAwait(false)));
        }

        return done;
    }

    private CustodyEvent EventFor(PlannedAppend planned, long item, long seed)
    {
        var author = planned.Author switch
        {
            PlannedAuthor.System => CustodyAuthor.System,
            PlannedAuthor.Active => CustodyAuthor.Person(people.Active),
            PlannedAuthor.Deactivated => CustodyAuthor.Person(people.Deactivated),
            PlannedAuthor.Unknown => CustodyAuthor.Person(long.MaxValue),
            _ => throw new ArgumentOutOfRangeException(nameof(planned)),
        };
        return planned.Corrects
            ? CustodyEvent.Correction(author, Item(item), seed)
            : CustodyEvent.Of(author, Item(item), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
    }

    private async Task<CustodyResult> SettleAsync(CustodyEvent pending)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            var result = await AppendLimitedAsync(pending).ConfigureAwait(false);
            if (result.Outcome != CustodyOutcome.Conflict || waited.Elapsed > Patience)
            {
                return result;
            }

            await Task.Delay(RandomNumberGenerator.GetInt32(0, 8)).ConfigureAwait(false);
        }
    }

    private async Task<CustodyResult> AppendLimitedAsync(CustodyEvent pending)
    {
        await InFlight.WaitAsync().ConfigureAwait(false);
        try
        {
            return await chain.AppendAsync(pending, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _ = InFlight.Release();
        }
    }
}
