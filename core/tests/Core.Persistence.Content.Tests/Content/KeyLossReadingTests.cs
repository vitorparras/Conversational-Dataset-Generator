using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// What a reading is told before anything is recorded (issue 67): a label the installation did not load is named and records nothing (CA-1), and, once the loss of the key was declared, the reversal
/// map and the raw text under the scope are refused with the reason and record nothing, while the rest reads as before (CA-3, CA-4).
/// </summary>
[Trait("Category", "Database")]
public sealed class KeyLossReadingTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Entries = "mapa de reversao sintetico zqx";
    private const string EventCount = "SELECT count(*) FROM public.custody_event";

    [DatabaseTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Ca1AnEnvelopeUnderALabelNotLoadedIsToldAsTheLabelMissingNamingItAndRecordsNothing(bool isRaw)
    {
        var unloaded = isRaw ? "texto-bruto/1/v1" : "mapa-reversao/1/v1";
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(1, item, Entries, TestContext.Current.CancellationToken);
        using var others = SyntheticCustody.Holding(ContentWorld.Labels.Where(label => label != unloaded));

        var result = await world.ReaderWith(others).ReadAsync(
            new ContentRead(CustodyAuthor.System, isRaw ? ContentColumns.RawText : ContentColumns.Entries, 1, isRaw ? item : map), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.LabelMissing, unloaded, null, 0L), (result.Outcome, result.Label, result.Text, await world.ScalarAsync(EventCount)));
    }

    [DatabaseFact]
    public async Task Ca1ARowThatIsMissingOrEmptyKeepsItsOwnOutcomeWhateverLabelsAreLoaded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        using var none = SyntheticCustody.Holding("mapa-reversao/2/v1");
        var reader = world.ReaderWith(none);

        var missing = await reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item + 100), TestContext.Current.CancellationToken);
        var empty = await reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.SanitizedText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.NotFound, ContentReadOutcome.Empty), (missing.Outcome, empty.Outcome));
    }

    [DatabaseFact]
    public async Task Ca3TheReversalMapUnderTheScopeOfALossIsRefusedWithTheReasonAndNothingIsRecordedWhileAnotherDomainReads()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var lost = await world.Writer.AddReversalMapAsync(1, item, Entries, TestContext.Current.CancellationToken);
        var other = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var kept = await world.Writer.AddReversalMapAsync(2, other, Entries, TestContext.Current.CancellationToken);
        await DeclareAsync(world, "mapa-reversao/1/v1");

        var refused = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 1, lost), TestContext.Current.CancellationToken);
        var read = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 2, kept), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.KeyLossDeclared, "mapa-reversao/1/v1", null), (refused.Outcome, refused.Label, refused.Text));
        Assert.Equal((ContentReadOutcome.Read, Entries), (read.Outcome, read.Text));
        Assert.Equal(0L, await world.ScalarAsync($"SELECT count(*) FROM public.custody_event WHERE step = 'reversal_entries_read' AND item_id = {lost}"));
    }

    [DatabaseFact]
    public async Task Ca4TheRawTextUnderTheScopeOfALossIsRefusedWithTheSameReasonAndNothingIsRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        await DeclareAsync(world, "texto-bruto/1/v1");

        var raw = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.KeyLossDeclared, "texto-bruto/1/v1", null), (raw.Outcome, raw.Label, raw.Text));
        Assert.Equal(0L, await world.ScalarAsync("SELECT count(*) FROM public.custody_event WHERE step = 'raw_text_read'"));
    }

    [DatabaseFact]
    public async Task Ca3ALossOfTheWholeKeyRefusesEveryReadingOfWhatIsSealed()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(2, item, Entries, TestContext.Current.CancellationToken);
        await DeclareAsync(world, "master");

        var raw = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 2, item), TestContext.Current.CancellationToken);
        var entries = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 2, map), TestContext.Current.CancellationToken);

        Assert.Equal(("master", "master", ContentReadOutcome.KeyLossDeclared, ContentReadOutcome.KeyLossDeclared), (raw.Label, entries.Label, raw.Outcome, entries.Outcome));
    }

    [DatabaseFact]
    public async Task Ca2TheDeclarationIsAnEventOfTheChainWithTheScopeAndWhatWasTriedAndTheChainVerifies()
    {
        await using var world = await ContentWorld.CreateAsync();

        await DeclareAsync(world, "texto-bruto/1/v1");

        var written = await world.ScalarAsync(
            "SELECT item_kind || ',' || item_id || ',' || step || ',' || reason || ',' || reference FROM public.custody_event WHERE step = 'key_loss_declared'");
        var verified = await new CustodyVerifier(world.DataSource, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(("master_key,3,key_loss_declared,administrative_act,texto-bruto/1/v1", true), (written, verified.Passed));
    }

    private static async Task DeclareAsync(ContentWorld world, string scope)
    {
        Assert.True(KeyLossScope.TryRead(scope, out var read));
        var written = await new CustodyChain(world.DataSource).AppendAsync(
            CustodyEvent.KeyLossDeclared(read!, KeyLossAttempts.SafekeepingCopy | KeyLossAttempts.PreviousSafekeepingCopy), TestContext.Current.CancellationToken);
        Assert.Equal(CustodyOutcome.Appended, written.Outcome);
    }
}
