using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>The message of a failure the chain does not know has the same words for every author, and nothing an author or a caller wrote (#58, CA-5).</summary>
[Trait("Category", "Database")]
public sealed class CustodyFailureContentTests
{
    private static readonly CustodyAuthor[] Authors = [CustodyAuthor.System, CustodyAuthor.Person(1), CustodyAuthor.Person(long.MaxValue)];

    private static readonly (CustodyStep Step, string Name)[] Steps =
    [
        (CustodyStep.PersonCreated, "person_created"),
        (CustodyStep.PersonDeactivated, "person_deactivated"),
        (CustodyStep.PersonFunctionChanged, "person_function_changed"),
        (CustodyStep.PersonPasswordSet, "person_password_set"),
    ];

    [DatabaseFact]
    public async Task MessageNamesOnlyTheItemTheStepAndTheReasonWhoeverSigns()
    {
        var (scratch, dataSource) = await CustodyDatabase.DatabaseWithoutTableAsync();
        await using var disposeScratch = scratch;
        await using var disposeSource = dataSource;
        var chain = new CustodyChain(dataSource);
        var messages = new List<string>();

        foreach (var author in Authors)
        {
            foreach (var (step, _) in Steps)
            {
                var pending = CustodyEvent.Of(author, new CustodyItem(CustodyItemKind.Person, 6100), step, CustodyReason.AdministrativeAct);
                messages.Add((await Assert.ThrowsAsync<InvalidOperationException>(() => chain.AppendAsync(pending, TestContext.Current.CancellationToken))).Message);
            }
        }

        var forOneAuthor = Steps.Select(step => $"The custody chain refused the event (SQLSTATE 42P01) about person 6100, step {step.Name}, reason administrative_act.");
        Assert.Equal(Enumerable.Repeat(forOneAuthor, Authors.Length).SelectMany(messagesOfOne => messagesOfOne), messages);
    }

    [DatabaseFact]
    public async Task MessageOfACorrectionNamesTheCorrectionAndItsReason()
    {
        var (scratch, dataSource) = await CustodyDatabase.DatabaseWithoutTableAsync();
        await using var disposeScratch = scratch;
        await using var disposeSource = dataSource;
        var pending = CustodyEvent.Correction(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 6200), 3);

        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => new CustodyChain(dataSource).AppendAsync(pending, TestContext.Current.CancellationToken));

        Assert.Equal("The custody chain refused the event (SQLSTATE 42P01) about person 6200, step correction, reason correction_of_record.", failed.Message);
    }
}
