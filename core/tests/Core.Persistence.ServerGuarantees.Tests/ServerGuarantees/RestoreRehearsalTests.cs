using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The rehearsal of a restore (issue 66, CA-2 to CA-4 and CA-6): a copy made by the copy of the data and put back by the functions of the load passes, and the chain records that it did;
/// a copy put back without a guarantee fails and the record names it; the seal of another chain fails; and nothing in the whole of it, from the copy to the check, reads content.
/// </summary>
[Trait("Category", "Database")]
public sealed class RestoreRehearsalTests
{
    private const string Rehearsals = "SELECT reason || '|' || item_id || '|' || reference || '|' || actor_system FROM public.custody_event WHERE step = 'restore_rehearsed' ORDER BY sequence";
    private const string Reads = "SELECT count(*)::text FROM public.custody_event WHERE step IN ('raw_text_read', 'sanitized_text_read', 'reversal_entries_read')";

    [DatabaseFact]
    public async Task Ca2CopyPutBackPassesTheRehearsalAndTheChainRecordsThatItPassed()
    {
        await using var copied = await CopiedWorld.CreateAsync();
        await using var restored = await RestoredCopy.LoadAsync(copied.Folder);

        var result = await new RestoreRehearsal(restored.App, TimeProvider.System).RehearseAsync(copied.Result.Seal!, TestContext.Current.CancellationToken);

        Assert.Equal((RehearsalFindings.None, CustodyOutcome.Appended, "held"), (result.Findings, result.Recorded.Outcome, result.Names));
        Assert.True(result.Passed);
        Assert.Equal(["verification_passed|0|held|true"], await restored.Database.FirstColumnAsOwnerAsync(Rehearsals));
    }

    [DatabaseFact]
    public async Task Ca3CopyPutBackWithoutAGuaranteeFailsAndTheRecordNamesIt()
    {
        await using var copied = await CopiedWorld.CreateAsync();
        await using var restored = await RestoredCopy.LoadAsync(copied.Folder);
        await restored.Database.RunAsOwnerAsync("ALTER TABLE content.item_text NO FORCE ROW LEVEL SECURITY");

        var result = await new RestoreRehearsal(restored.App, TimeProvider.System).RehearseAsync(copied.Result.Seal!, TestContext.Current.CancellationToken);

        Assert.Equal((RehearsalFindings.Policy, CustodyOutcome.Appended, "policy"), (result.Findings, result.Recorded.Outcome, result.Names));
        Assert.False(result.Passed);
        Assert.Equal([$"verification_failed|{(long)RehearsalFindings.Policy}|policy|true"], await restored.Database.FirstColumnAsOwnerAsync(Rehearsals));
    }

    [DatabaseFact]
    public async Task Ca3SealOfAnotherChainFailsTheRehearsalWhileTheLinksStillHold()
    {
        await using var copied = await CopiedWorld.CreateAsync();
        await using var restored = await RestoredCopy.LoadAsync(copied.Folder);
        var other = ChainSeal.Of(copied.Result.Seal!.SealedAt, copied.Result.Seal.Events, new byte[32]);

        var result = await new RestoreRehearsal(restored.App, TimeProvider.System).RehearseAsync(other, TestContext.Current.CancellationToken);

        Assert.Equal((RehearsalFindings.Seal, SealOutcome.DigestDiffers, true), (result.Findings, result.Verification.Seal!.Outcome, result.Verification.Passed));
        Assert.Equal([$"verification_failed|{(long)RehearsalFindings.Seal}|seal|true"], await restored.Database.FirstColumnAsOwnerAsync(Rehearsals));
    }

    [DatabaseFact]
    public async Task Ca3ChainWithABrokenLinkFailsNamingTheChainAndTheChainTakesNoEventOfTheResult()
    {
        await using var copied = await CopiedWorld.CreateAsync();
        await using var restored = await RestoredCopy.LoadAsync(copied.Folder);
        await restored.Database.ExecuteAsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 2; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed");

        var result = await new RestoreRehearsal(restored.App, TimeProvider.System).RehearseAsync(copied.Result.Seal!, TestContext.Current.CancellationToken);

        Assert.True(result.Findings.HasFlag(RehearsalFindings.Chain));
        Assert.Contains("chain", result.Names, StringComparison.Ordinal);
        Assert.Equal(CustodyOutcome.ChainBroken, result.Recorded.Outcome);
        Assert.Null(result.Recorded.Sequence);
        Assert.Empty(await restored.Database.FirstColumnAsOwnerAsync(Rehearsals));
    }

    [DatabaseFact]
    public async Task Ca6NoEventOfReadingIsWrittenFromTheCopyToTheRehearsalInEitherDatabase()
    {
        await using var copied = await CopiedWorld.CreateAsync();
        await using var restored = await RestoredCopy.LoadAsync(copied.Folder);

        _ = await new RestoreRehearsal(restored.App, TimeProvider.System).RehearseAsync(copied.Result.Seal!, TestContext.Current.CancellationToken);

        Assert.Equal(["0"], await copied.World.OwnerColumnAsync(Reads));
        Assert.Equal(["0"], await restored.Database.FirstColumnAsOwnerAsync(Reads));
        Assert.Equal("1", (string?)await InDomainCountAsync(restored));
    }

    private static async Task<object?> InDomainCountAsync(RestoredCopy restored)
    {
        await using var transaction = await ContentTransaction.BeginAsync(restored.App, 1, TestContext.Current.CancellationToken);
        await using var command = transaction.CreateCommand("SELECT count(*)::text FROM content.item_text WHERE emptied_at IS NULL");
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
