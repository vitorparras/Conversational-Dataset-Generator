using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// How the database holds what the sanitization left with an item (issues 87, 92, 93 and 98): the sanitized text and the trail sealed with the purpose of the raw text, the version of the
/// list of steps stored once per domain, the counts in clear and only as numbers, the four together or none, and the event of the batch carrying the version and never changing.
/// </summary>
[Trait("Category", "Database")]
public sealed class SanitizationStorageTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Sanitized = "texto sanitizado sintetico zqx";
    private const string Trail = "SELECT sanitization_trail FROM content.item_text WHERE id = @id";
    private const string CheckViolation = "23514";
    private const string KeptWhoeverWrites = "CE004";
    private const string Empty = "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL, "
        + "occurrences = NULL, dek_epoch = NULL, dek_wrapped = NULL, sanitization_version = NULL, sanitization_trail = NULL, sanitization_counts = NULL, "
        + "sanitization_untreated = NULL WHERE id = @id RETURNING id";
    private const string SanitizedEvent = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason, reference) "
        + "VALUES (NULL, true, 'ingestion_batch', 1, 'batch_sanitized', 'administrative_act', @reference) RETURNING sequence";

    [DatabaseFact]
    public async Task SanitizedAndTrailAreSealedWithTheRawPurpose()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(2, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);

        var labels = new[]
        {
            EnvelopeCipher.LabelOf(await world.StoredAsync(2, ContentQueries.ReadSanitizedText, item)),
            EnvelopeCipher.LabelOf(await world.StoredAsync(2, Trail, item)),
        };

        Assert.Equal(["texto-bruto/2/v1", "texto-bruto/2/v1"], labels);
    }

    [DatabaseFact]
    public async Task DatabaseCopyHasNoReadableSanitizedText()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);

        var dump = await world.Database.DumpAsync();

        Assert.Equal(
            (true, false, false),
            (dump.Contains(ItemTexts.Version, StringComparison.Ordinal), dump.Contains(Sanitized, StringComparison.Ordinal), dump.Contains("\"steps\":[[", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task ClearTextColumnHoldsNoMaterialAtTheEndOfThePhase()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);

        var held = await world.OwnerInDomainAsync(1, "SELECT (pseudonymized_text IS NULL)::text || ':' || (sanitized_text IS NOT NULL)::text FROM content.item_text WHERE id = @id", ("id", item));

        Assert.Equal("true:true", held);
    }

    [DatabaseFact]
    public async Task SanitizedContentCannotBeFilteredOrOrderedInTheDatabase()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, "aaa sanitizado sintetico zqx"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, "zzz sanitizado sintetico zqx"), TestContext.Current.CancellationToken);

        // An order by the column compares sealed bytes: with no byte of the text in them, neither a filter nor an order can follow the content.
        var byBytes = await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.item_text WHERE position(convert_to('sanitizado', 'UTF8') IN sanitized_text) > 0");
        var byText = await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.item_text WHERE encode(sanitized_text, 'escape') LIKE '%sanitizado%'");
        var sealedItems = await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.item_text WHERE sanitized_text IS NOT NULL");

        Assert.Equal((0L, 0L, 2L), ((long)byBytes!, (long)byText!, (long)sealedItems!));
    }

    [DatabaseFact]
    public async Task SanitizationColumnsComeTogetherOrNotAtAll()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);
        const string Alone = "INSERT INTO content.item_text (domain_id, batch_id, raw_text, sanitized_text, dek_epoch, dek_wrapped) "
            + "VALUES (1, 1, decode('a1', 'hex'), decode('b2', 'hex'), 1, decode('c3', 'hex')) RETURNING id";

        var inserted = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Alone));
        var updated = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "UPDATE content.item_text SET sanitization_counts = NULL WHERE id = @id", ("id", item)));
        var miscounted = await Assert.ThrowsAsync<PostgresException>(() => world.RealWriter.AddItemTextAsync(
            1, 1, new NewItemText(Raw, null, []) { Sanitization = ItemTexts.Sanitization(Sanitized) with { Counts = [0, 0], Untreated = [0, 0] } }, TestContext.Current.CancellationToken));

        Assert.Equal(
            ((CheckViolation, "item_text_sanitization_together"), KeptWhoeverWrites, CheckViolation),
            ((inserted.SqlState, inserted.ConstraintName), updated.SqlState, miscounted.SqlState));
    }

    [DatabaseFact]
    public async Task SameVersionFromTwoBatchesIsStoredOnce()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = (long)(await world.OwnerInDomainAfterAsync(
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_is_born_open; ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_open_needs_event",
            1,
            "INSERT INTO content.ingestion_batch (domain_id, state, opened_by, condition, closed_at) SELECT 1, 'accepted', min(id), 'restrita', now() FROM public.person RETURNING id"))!;

        _ = await world.RealWriter.AddItemTextAsync(1, 1, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);
        _ = await world.RealWriter.AddItemTextAsync(1, second, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddItemTextAsync(2, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);

        const string Stored = "SELECT string_agg(domain_id || ':' || steps, ',') FROM content.sanitization_version WHERE version = @version";
        var first = await world.OwnerInDomainAsync(1, Stored, ("version", ItemTexts.Version));
        var other = await world.OwnerInDomainAsync(2, Stored, ("version", ItemTexts.Version));

        Assert.Equal(("1:destinations@1", "2:destinations@1"), (first, other));
    }

    [DatabaseFact]
    public async Task SanitizationEventCarriesTheVersionAndCannotChange()
    {
        await using var world = await ContentWorld.CreateAsync();

        var sequence = (long)(await world.InDomainAsync(1, SanitizedEvent, ("reference", ItemTexts.Version)))!;
        var withoutVersion = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, SanitizedEvent, ("reference", DBNull.Value)));
        var notAVersion = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, SanitizedEvent, ("reference", "destinations@1")));
        await world.Database.RunAsOwnerAsync("GRANT UPDATE ON public.custody_event TO owner");
        var changed = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(
            "UPDATE public.custody_event SET reference = @other WHERE sequence = @sequence", ("other", new string('0', 64)), ("sequence", sequence)));

        Assert.Equal(ItemTexts.Version, await world.ScalarAsync("SELECT reference FROM public.custody_event WHERE sequence = @sequence", ("sequence", sequence)));
        Assert.Equal((CheckViolation, CheckViolation), (withoutVersion.SqlState, notAVersion.SqlState));
        Assert.Equal("CE001", changed.SqlState);
    }

    [DatabaseFact]
    public async Task BatchSanitizationCountsReturnOnlyNumbers()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, null, []) { Sanitization = ItemTexts.Sanitization(Sanitized) with { Counts = [3] } }, TestContext.Current.CancellationToken);
        _ = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw, null, []) { Sanitization = ItemTexts.Sanitization(Sanitized) with { Counts = [5] } }, TestContext.Current.CancellationToken);

        var counts = await world.InDomainAsync(
            1, "SELECT string_agg(sanitization_version || '|' || step_name || '|' || step_edits || '|' || items_changed, ',') FROM public.batch_sanitization_counts(1, 1)");

        Assert.Equal($"{ItemTexts.Version}|destinations|3|1", counts);
    }

    [DatabaseFact]
    public async Task BatchSanitizationCountsLeaveOutOtherBatchesAndEmptiedItems()
    {
        await using var world = await ContentWorld.CreateAsync();
        var second = (long)(await world.OwnerInDomainAfterAsync(
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_is_born_open; ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_open_needs_event",
            1,
            "INSERT INTO content.ingestion_batch (domain_id, state, opened_by, condition, closed_at) SELECT 1, 'accepted', min(id), 'restrita', now() FROM public.person RETURNING id"))!;
        static NewItemText Counted(int edits) => new(Raw, null, []) { Sanitization = ItemTexts.Sanitization(Sanitized) with { Counts = [edits] } };
        _ = await world.RealWriter.AddItemTextAsync(1, 1, Counted(3), TestContext.Current.CancellationToken);
        _ = await world.RealWriter.AddItemTextAsync(1, second, Counted(5), TestContext.Current.CancellationToken);
        var emptied = await world.RealWriter.AddItemTextAsync(1, 1, Counted(7), TestContext.Current.CancellationToken);
        _ = await world.OwnerInDomainAfterAsync(ContentWorld.EmptiedEvent, 1, Empty, ("id", emptied));

        const string Counts = "SELECT string_agg(step_edits || '|' || items_changed, ',') FROM public.batch_sanitization_counts(@batch, 1)";
        var first = await world.InDomainAsync(1, Counts, ("batch", 1L));
        var other = await world.InDomainAsync(1, Counts, ("batch", second));

        Assert.Equal(("3|1", "5|1"), (first, other));
    }

    [DatabaseTheory]
    [InlineData("sanitization_version", "@version", "item_text_sanitization_together")]
    [InlineData("sanitization_trail", "decode('d4', 'hex')", "item_text_sanitization_together")]
    [InlineData("sanitization_counts", "'{1}'", "item_text_sanitization_together")]
    [InlineData("sanitized_text, sanitization_version, sanitization_trail", "decode('b2', 'hex'), @version, decode('d4', 'hex')", "item_text_sanitization_together")]
    [InlineData("sanitized_text, sanitization_version, sanitization_counts", "decode('b2', 'hex'), @version, '{1}'", "item_text_sanitization_together")]
    [InlineData("sanitized_text, sanitization_version, sanitization_trail, sanitization_counts", "decode('b2', 'hex'), @version, decode('d4', 'hex'), '{-1}'", "item_text_sanitization_counts")]
    [InlineData("sanitized_text, sanitization_version, sanitization_trail, sanitization_counts", "decode('b2', 'hex'), @version, decode('d4', 'hex'), '{NULL}'", "item_text_sanitization_counts")]
    public async Task SanitizationColumnsMissingOneOrCountingBelowZeroAreRefused(string columns, string values, string constraint)
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, Sanitized), TestContext.Current.CancellationToken);
        var insert = $"INSERT INTO content.item_text (domain_id, batch_id, raw_text, dek_epoch, dek_wrapped, {columns}) "
            + $"VALUES (1, 1, decode('a1', 'hex'), 1, decode('c3', 'hex'), {values}) RETURNING id";

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, insert, ("version", ItemTexts.Version)));

        Assert.Equal((CheckViolation, constraint), (refused.SqlState, refused.ConstraintName));
    }
}
