using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
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
    private const string ReadSteps = "SELECT step FROM public.custody_event WHERE step LIKE '%read' AND actor_system AND reason = 'content_read' ORDER BY step";

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
        await world.MakeCuratorAsync();
        var person = (long)(await world.InDomainAsync(1, "SELECT id FROM public.person WHERE name = 'zqx-active'"))!;
        var request = new ItemEmptying(CustodyAuthor.Person(person), 1, item, EmptyingMotive.ContractRequirement, "PROT-2026/0042");
        _ = await new ContentEmptier(world.DataSource).EmptyAsync(request, token);

        var verification = await world.Verifier().VerifyAsync(token);
        var differences = await AuditFidelity.DifferencesAsync(world.DataSource, from);

        Assert.Equal(GuaranteeKinds.None, verification.Missing);
        Assert.Empty(await world.OwnerColumnAsync("SELECT step FROM public.custody_event WHERE step LIKE '%read'"));
        Assert.DoesNotContain(differences, difference => difference.Role == "app");
    }

    [DatabaseFact]
    public async Task SampleOfADomainUnderTheLossDeclaredIsPassedOverForTheOneOutsideItAndTheGuaranteeHolds()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        await WriteItemAndMapAsync(world, 1);
        await WriteItemAndMapAsync(world, 2);
        await DeclareAsync(world, "texto-bruto/1/v1");
        await DeclareAsync(world, "mapa-reversao/1/v1");

        var verification = await world.Verifier().VerifyAsync(token);

        Assert.Equal((GuaranteeKinds.None, false), (verification.Missing, verification.NoSampleOutsideLoss));
        Assert.Equal(["raw_text_read", "reversal_entries_read"], await world.OwnerColumnAsync(ReadSteps));
        Assert.Equal(["2", "2"], await world.OwnerColumnAsync("SELECT item_id::text FROM public.custody_event WHERE step LIKE '%read' ORDER BY step"));
    }

    [DatabaseFact]
    public async Task EverythingUnderTheLossDeclaredIsToldAsNoSampleOutsideItAndNotAsAGuaranteeMissing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        await WriteItemAndMapAsync(world, 1);
        await DeclareAsync(world, "master");

        var verification = await world.Verifier().VerifyAsync(token);

        Assert.Equal((GuaranteeKinds.None, true, CustodyOutcome.Appended), (verification.Missing, verification.NoSampleOutsideLoss, verification.Recorded.Outcome));
        Assert.Equal(["verification_passed|0"], await world.OwnerColumnAsync(Verification));
        Assert.Empty(await world.OwnerColumnAsync(ReadSteps));
    }

    [DatabaseFact]
    public async Task RecordOutsideTheLossDeclaredThatDoesNotOpenKeepsTheGuaranteeMissing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        await WriteItemAndMapAsync(world, 1);
        _ = await world.Writer.AddItemTextAsync(2, new NewItemText("texto bruto sintetico zqx"), token);
        await DeclareAsync(world, "mapa-reversao/1/v1");

        var verification = await world.VerifierOver(new SyntheticKeySource(["texto-bruto/1/v1", "mapa-reversao/1/v1", "mapa-reversao/2/v1"])).VerifyAsync(token);

        Assert.Equal((GuaranteeKinds.EncryptedSample, false), (verification.Missing, verification.NoSampleOutsideLoss));
        Assert.Equal([$"verification_failed|{(long)GuaranteeKinds.EncryptedSample}"], await world.OwnerColumnAsync(Verification));
    }

    [DatabaseTheory]
    [InlineData(16, false)]
    [InlineData(17, true)]
    public async Task LossDeclaredOverMoreRowsThanTheSearchPassesOverIsToldAsStoppedAtTheLimitAndNotAsNoSampleOutsideIt(int rows, bool stopped)
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        for (var row = 0; row < rows; row++)
        {
            _ = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx"), token);
        }

        await DeclareAsync(world, "texto-bruto/1/v1");

        var verification = await world.Verifier().VerifyAsync(token);

        Assert.Equal((GuaranteeKinds.None, !stopped, stopped), (verification.Missing, verification.NoSampleOutsideLoss, verification.SearchStopped));
    }

    [DatabaseFact]
    public async Task RecordSealedUnderTheLabelOfAnotherDomainIsTold()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(2, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        await world.RunAsSuperuserAsync(
            "INSERT INTO content.item_text (domain_id, raw_text, sanitized_text, pseudonymized_text, dek_epoch, dek_wrapped) "
            + "SELECT 1, raw_text, sanitized_text, pseudonymized_text, dek_epoch, dek_wrapped FROM content.item_text WHERE domain_id = 2");

        var verification = await world.Verifier().VerifyAsync(token);

        Assert.Equal(GuaranteeKinds.EncryptedSample, verification.Missing);
        Assert.Equal([$"verification_failed|{(long)GuaranteeKinds.EncryptedSample}"], await world.OwnerColumnAsync(Verification));
    }

    [DatabaseFact]
    public async Task RecordSealedUnderALabelTheInstallationDidNotLoadMakesTheSampleMissingWithoutReadingAnything()
    {
        var token = TestContext.Current.CancellationToken;
        await using var world = await FidelityWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto sintetico zqx"), token);

        var verification = await world.VerifierOver(new SyntheticKeySource(["texto-bruto/2/v1", "mapa-reversao/2/v1"])).VerifyAsync(token);

        Assert.Equal(GuaranteeKinds.EncryptedSample, verification.Missing);
        Assert.Empty(await world.OwnerColumnAsync("SELECT step FROM public.custody_event WHERE step LIKE '%read'"));
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

    private static async Task WriteItemAndMapAsync(FidelityWorld world, int domain)
    {
        var token = TestContext.Current.CancellationToken;
        var item = await world.Writer.AddItemTextAsync(domain, new NewItemText("texto bruto sintetico zqx", "texto sanitizado sintetico zqx"), token);
        _ = await world.Writer.AddReversalMapAsync(domain, item, "mapa sintetico zqx", token);
    }

    private static async Task DeclareAsync(FidelityWorld world, string scope)
    {
        Assert.True(KeyLossScope.TryRead(scope, out var read));
        var written = await new CustodyChain(world.DataSource).AppendAsync(
            CustodyEvent.KeyLossDeclared(read!, KeyLossAttempts.SafekeepingCopy | KeyLossAttempts.PreviousSafekeepingCopy), TestContext.Current.CancellationToken);
        Assert.Equal(CustodyOutcome.Appended, written.Outcome);
    }
}
