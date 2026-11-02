using Core.Persistence.Custody;
using Core.Tests.Support.Harness;

namespace Core.Persistence.Tests.Custody;

/// <summary>
/// The invariants of the chain under writers that run at once: requisito 019 (docs/requisitos/019-custodia-encadeada.md) wants a chain in which no link has two
/// successors, and decisão 0007 (docs/decisoes/0007-garantia-de-qualidade.md) wants it proved by a property and not by a few runs.
/// </summary>
internal sealed class CustodyChainInvariants
{
    private readonly PropertyRunner runner;
    private readonly ConcurrentWriters writers;
    private readonly Func<Task<IReadOnlyList<EventRow>>> readChain;
    private readonly PlannedPeople people;

    internal CustodyChainInvariants(PropertyRunner runner, ConcurrentWriters writers, Func<Task<IReadOnlyList<EventRow>>> readChain, PlannedPeople people)
    {
        this.runner = runner;
        this.writers = writers;
        this.readChain = readChain;
        this.people = people;
    }

    /// <summary>However the writers meet, the chain ends numbered without a gap, no link with two successors, and every answer told what the chain holds.</summary>
    internal void NoLinkEndsWithTwoSuccessorsAndEveryAnswerIsTheChain() =>
        runner.Check(WriterPlan.Generator, plan =>
        {
            var run = writers.Run(plan);
            var rows = readChain().GetAwaiter().GetResult();

            ChainIntegrity.Holds(rows);
            AnswersAreWhatTheChainHolds(run, rows);
            EveryEventIsSignedAndCorrectsAsAsked(run, rows);
        });

    private static void AnswersAreWhatTheChainHolds(CaseRun run, IEnumerable<EventRow> rows)
    {
        var own = rows.Where(row => ConcurrentWriters.Owns(run, row.ItemId));
        var written = run.Answers.Where(answer => answer.Result.Outcome == CustodyOutcome.Appended).Select(answer => answer.Result.Sequence);

        Assert.All(run.Answers, answer => Assert.Equal(Expected(answer.Plan.Author), answer.Result.Outcome));
        Assert.Equal(written.Append(run.Seed).Order(), own.Select(row => (long?)row.Sequence).Order());
    }

    private static CustodyOutcome Expected(PlannedAuthor author) => author switch
    {
        PlannedAuthor.Deactivated => CustodyOutcome.ActorDeactivated,
        PlannedAuthor.Unknown => CustodyOutcome.ActorUnknown,
        PlannedAuthor.System or PlannedAuthor.Active => CustodyOutcome.Appended,
        _ => throw new ArgumentOutOfRangeException(nameof(author)),
    };

    private void EveryEventIsSignedAndCorrectsAsAsked(CaseRun run, IEnumerable<EventRow> rows)
    {
        var stored = rows.ToDictionary(row => row.ItemId);
        var appended = run.Answers.Where(answer => answer.Result.Outcome == CustodyOutcome.Appended);

        Assert.All(
            appended,
            answer => Assert.Equal(
                (SignedBy(answer.Plan.Author), answer.Plan.Corrects ? run.Seed : default(long?)),
                (stored[answer.Item].ActorPersonId, stored[answer.Item].Corrects)));
    }

    private long? SignedBy(PlannedAuthor author) => author == PlannedAuthor.Active ? people.Active : null;
}
