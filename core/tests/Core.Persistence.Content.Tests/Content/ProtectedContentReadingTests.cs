using Core.Persistence.Tests.Fixtures;
using Core.Persistence.Content;
using Core.Persistence.Custody;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>Reading protected content is an event of the chain: who, which item, which step and why, recorded before the content is opened and never carrying it.</summary>
[Trait("Category", "Database")]
public sealed class ProtectedContentReadingTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Sanitized = "texto sanitizado sintetico zqx";
    private const string Entries = "mapa de reversao sintetico zqx";
    private const string Events =
        "SELECT coalesce(string_agg(item_kind || ',' || item_id || ',' || step || ',' || reason || ',' || coalesce(actor_person_id::text, 'system'), ';' ORDER BY sequence), '') "
        + "FROM public.custody_event";
    private const string EventRows = "SELECT coalesce(string_agg(custody_event::text, ';'), '') FROM public.custody_event";

    [DatabaseFact]
    public async Task RawTextIsReadAndTheEventNamesWhoReadWhichItemAndStep()
    {
        await using var world = await ContentWorld.CreateAsync();
        var reader = await world.PersonAsync("zqx-active");
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.Person(reader), ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.Read, Raw, $"item_text,{item},raw_text_read,content_read,{reader}"), (result.Outcome, result.Text, await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task SanitizedTextGoesThroughTheSamePointOfReadingAsTheRawText()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Sanitized), TestContext.Current.CancellationToken);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.SanitizedText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((Sanitized, $"item_text,{item},sanitized_text_read,content_read,system"), (result.Text, await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task ReversalMapIsReadAndTheEventIsAboutTheMap()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(2, item, Entries, TestContext.Current.CancellationToken);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 2, map), TestContext.Current.CancellationToken);

        Assert.Equal((Entries, $"reversal_map,{map},reversal_entries_read,content_read,system"), (result.Text, await world.ScalarAsync(Events)));
    }

    [DatabaseFact]
    public async Task EventCarriesNoContentOfWhatWasRead()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Sanitized), TestContext.Current.CancellationToken);
        _ = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        var rows = (string)(await world.ScalarAsync(EventRows))!;

        Assert.Equal([false, false], [rows.Contains(Raw, StringComparison.Ordinal), rows.Contains(Sanitized, StringComparison.Ordinal)]);
    }

    [DatabaseFact]
    public async Task EachReadingAddsAnEventOfItsOwn()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var request = new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, item);
        _ = await world.Reader.ReadAsync(request, TestContext.Current.CancellationToken);
        _ = await world.Reader.ReadAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(2L, await world.ScalarAsync("SELECT count(*) FROM public.custody_event"));
    }

    [DatabaseFact]
    public async Task ReadingThatTheChainDoesNotRecordReturnsNoText()
    {
        await using var world = await ContentWorld.CreateAsync();
        var deactivated = await world.PersonAsync("zqx-gone");
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.Person(deactivated), ContentColumns.RawText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.NotRecorded, null, CustodyOutcome.ActorDeactivated), (result.Outcome, result.Text, result.Refusal?.Outcome));
    }

    [DatabaseFact]
    public async Task ItemOfAnotherDomainIsNotFoundAndNothingIsRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 2, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.NotFound, null, 0L), (result.Outcome, result.Text, await world.ScalarAsync("SELECT count(*) FROM public.custody_event")));
    }

    [DatabaseFact]
    public async Task ColumnThatHoldsNothingYetIsEmptyAndNothingIsRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var result = await world.Reader.ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.SanitizedText, 1, item), TestContext.Current.CancellationToken);

        Assert.Equal((ContentReadOutcome.Empty, null, 0L), (result.Outcome, result.Text, await world.ScalarAsync("SELECT count(*) FROM public.custody_event")));
    }

    [Fact]
    public async Task ReadingOfAColumnThatIsNotProtectedOrOfNothingIsRefusedBeforeAnyConnectionOpens()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        using var custody = SyntheticCustody.Holding("texto-bruto/1/v1");
        var reader = new ProtectedContentReader(dataSource, custody.Content, new CustodyChain(dataSource));
        var foreign = new ProtectedColumn("item_text", "pseudonymized_text", Core.Platform.Keys.LabelPurpose.RawText);

        Assert.Equal(
            ["request", "request"],
            [
                (await Assert.ThrowsAsync<ArgumentNullException>(() => reader.ReadAsync(null!, TestContext.Current.CancellationToken))).ParamName,
                (await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(new ContentRead(CustodyAuthor.System, foreign, 1, 1), TestContext.Current.CancellationToken))).ParamName,
            ]);
    }
}
