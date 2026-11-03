using Core.Persistence.Content;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Persistence.ServerGuarantees;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The conference of the guarantees against a real server (#68, CA-1 to CA-4): every guarantee held is recorded as passed with the mask zero; each guarantee removed is recorded as failed with its bit
/// and named, while the chain still closes; the role of the application made owner of a table is told; the sample opens, leaves its access in the chain, and a record under another label is told.
/// </summary>
[Trait("Category", "Database")]
public sealed class GuaranteeVerifierTests
{
    private const string Verification = "SELECT reason || '|' || item_id FROM public.custody_event WHERE step = 'verify_guarantees' ORDER BY sequence";

    [DatabaseFact]
    public async Task EveryGuaranteeHeldIsRecordedAsPassedByTheSystemWithTheMomentAndNothingMissing()
    {
        var before = DateTimeOffset.UtcNow;
        await using var world = await FidelityWorld.CreateAsync();

        var verification = await world.Verifier().VerifyAsync(TestContext.Current.CancellationToken);

        var recorded = await new RecordedVerificationReader(world.DataSource).LastGuaranteesAsync(TestContext.Current.CancellationToken);
        Assert.Equal((GuaranteeKinds.None, CustodyOutcome.Appended), (verification.Missing, verification.Recorded.Outcome));
        Assert.NotNull(recorded);
        Assert.Equal((true, 0L), (recorded.Passed, recorded.MissingMask));
        Assert.InRange(recorded.RecordedAt, before.AddSeconds(-1), DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.Equal(["verification_passed|0"], await world.OwnerColumnAsync(Verification));
        Assert.Equal(["true"], await world.OwnerColumnAsync("SELECT actor_system::text FROM public.custody_event WHERE step = 'verify_guarantees'"));
    }

    [DatabaseTheory]
    [InlineData("DROP POLICY domain_isolation ON content.item_text", GuaranteeKinds.RowPolicy)]
    [InlineData("ALTER TABLE content.item_text NO FORCE ROW LEVEL SECURITY", GuaranteeKinds.RowPolicy)]
    [InlineData("GRANT UPDATE ON content.item_text TO app", GuaranteeKinds.ColumnRevoke)]
    [InlineData("ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed", GuaranteeKinds.Immutability)]
    [InlineData("ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_emptied", GuaranteeKinds.Immutability)]
    public async Task GuaranteeRemovedIsRecordedAsFailedNamingItWhileTheChainStillCloses(string removal, GuaranteeKinds missing)
    {
        await using var world = await FidelityWorld.CreateAsync();
        await world.RunAsOwnerAsync(removal);

        var verification = await world.Verifier().VerifyAsync(TestContext.Current.CancellationToken);
        var chain = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal((missing, missing.ToString(), true), (verification.Missing, verification.Missing.ToString(), chain.Passed));
        Assert.Equal([$"verification_failed|{(long)missing}"], await world.OwnerColumnAsync(Verification));
    }

    [DatabaseFact]
    public async Task RoleOfTheApplicationMadeOwnerOfATableIsTold()
    {
        await using var world = await FidelityWorld.CreateAsync();
        await world.RunAsOwnerAsync("CREATE TABLE public.zqx_owned (a integer)");
        await world.RunAsSuperuserAsync("ALTER TABLE public.zqx_owned OWNER TO app");

        var verification = await world.Verifier().VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GuaranteeKinds.Privilege, verification.Missing);
        Assert.Equal([$"verification_failed|{(long)GuaranteeKinds.Privilege}"], await world.OwnerColumnAsync(Verification));
    }

    [DatabaseFact]
    public async Task SampleOpensAndEachReadingIsAnAccessOfTheSystemInTheChain()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa sintetico zqx", token);

        var verification = await world.Verifier().VerifyAsync(token);

        Assert.Equal(GuaranteeKinds.None, verification.Missing);
        Assert.Equal(
            ["raw_text_read", "reversal_entries_read"],
            await world.OwnerColumnAsync("SELECT step FROM public.custody_event WHERE step LIKE '%read' AND actor_system AND reason = 'content_read' ORDER BY step"));
    }

    [DatabaseFact]
    public async Task SampleReadsLeaveNoDifferenceBetweenTheServerLogAndTheChain()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa sintetico zqx", token);
        _ = await world.Verifier().VerifyAsync(token);

        var differences = await AuditFidelity.DifferencesAsync(world.DataSource, from);

        Assert.DoesNotContain(differences, difference => difference.Role == "app");
    }

    [DatabaseFact]
    public async Task SampleReadsLeaveNoDifferenceWhenADomainHoldsOnlyEmptiedItemsAndMaps()
    {
        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa sintetico zqx", token);
        await world.RunAsSuperuserAsync("UPDATE content.reversal_map SET emptied_at = now(), entries = NULL");
        await world.RunAsSuperuserAsync("UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL");

        var verification = await world.Verifier().VerifyAsync(token);
        var differences = await AuditFidelity.DifferencesAsync(world.DataSource, from);

        Assert.Equal(GuaranteeKinds.None, verification.Missing);
        Assert.Empty(await world.OwnerColumnAsync("SELECT step FROM public.custody_event WHERE step LIKE '%read'"));
        Assert.DoesNotContain(differences, difference => difference.Role == "app");
    }

    [DatabaseFact]
    public async Task RecordSealedUnderTheLabelOfAnotherDomainIsTold()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(2, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        await world.RunAsSuperuserAsync(
            "INSERT INTO content.item_text (domain_id, raw_text, sanitized_text, pseudonymized_text) "
            + "SELECT 1, raw_text, sanitized_text, pseudonymized_text FROM content.item_text WHERE domain_id = 2");

        var verification = await world.Verifier().VerifyAsync(token);

        Assert.Equal(GuaranteeKinds.EncryptedSample, verification.Missing);
        Assert.Equal([$"verification_failed|{(long)GuaranteeKinds.EncryptedSample}"], await world.OwnerColumnAsync(Verification));
    }

    [DatabaseFact]
    public async Task KeyThatCannotBeReadMakesTheSampleMissingWithoutReadingAnything()
    {
        await using var world = await FidelityWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx"), TestContext.Current.CancellationToken);

        var verification = await world.VerifierOver(LabelsSource.WithoutKey()).VerifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(GuaranteeKinds.EncryptedSample, verification.Missing);
        Assert.Empty(await world.OwnerColumnAsync("SELECT step FROM public.custody_event WHERE step LIKE '%read'"));
    }

    [DatabaseFact]
    public async Task ConferenceThatNeverRanLeavesNothingToReadSoNoResultIsNotAPass()
    {
        await using var world = await FidelityWorld.CreateAsync();

        var recorded = await new RecordedVerificationReader(world.DataSource).LastGuaranteesAsync(TestContext.Current.CancellationToken);

        Assert.Null(recorded);
    }
}
