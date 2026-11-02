using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Custody;

/// <summary>A failure the chain does not know is a defect and says where, with the item, the step and the reason, and nothing of the material or of a person (#58, CA-5).</summary>
[Trait("Category", "Database")]
public sealed class CustodyFailureTests
{
    [DatabaseFact]
    public async Task FailureOfTheServerNamesTheSqlStateTheItemTheStepAndTheReasonOnly()
    {
        var (scratch, dataSource) = await CustodyDatabase.DatabaseWithoutTableAsync();
        await using var disposeScratch = scratch;
        await using var disposeSource = dataSource;
        var chain = new CustodyChain(dataSource);
        var custodyEvent = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 6001), CustodyStep.PersonPasswordSet, CustodyReason.AdministrativeAct);

        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => chain.AppendAsync(custodyEvent, TestContext.Current.CancellationToken));

        Assert.Equal("The custody chain refused the event (SQLSTATE 42P01) about person 6001, step person_password_set, reason administrative_act.", failed.Message);
        Assert.Null(failed.InnerException);
    }
}
