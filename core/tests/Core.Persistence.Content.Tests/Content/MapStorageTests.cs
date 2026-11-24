using System.Text;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Custody;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// How the database holds the map of a pseudonymized item and the pseudonymized text (issues 101 and 102): sealed by the application under a purpose of its own and the domain, opened only
/// by the application with an event for each opening, never filtered nor ordered by the database, and absent from a copy of the database read without the secret.
/// </summary>
[Trait("Category", "Database")]
public sealed class MapStorageTests
{
    private const string MapOfItem = "SELECT id FROM content.reversal_map WHERE item_id = @id";
    private const string UndefinedFunction = "42883";
    private const string NotInRepertoire = "22021";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task DatabaseCopyHasNoReadableMapEntry()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(2);
        _ = await PseudonymizedAsync(world, 1, markers[0]);
        _ = await PseudonymizedAsync(world, 2, markers[1]);

        var dump = await world.Database.DumpAsync();

        // The label of the map goes in clear beside what it seals, so the dump holds the maps; what each marker replaced is in none of them, as text or as bytes.
        Assert.Equal((true, true), (dump.Contains(Hex("mapa-reversao/1/v1"), StringComparison.Ordinal), dump.Contains(Hex("mapa-reversao/2/v1"), StringComparison.Ordinal)));
        Assert.Equal(
            [false, false, false, false],
            markers.Select(PseudonymizedTexts.OriginalOf).SelectMany(original => new[] { original, Hex(original) }).Select(seeded => dump.Contains(seeded, StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task DatabaseCopyReadsNeitherSanitizedNorPseudonymized()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        _ = await PseudonymizedAsync(world, 1, marker);
        var pseudonymized = PseudonymizedTexts.PseudonymizedOf([marker]);

        var dump = await world.Database.DumpAsync();

        // The marker is stored in clear, as the source of the counts of the batch; the text that holds it is sealed with the key of the item, like the raw one.
        Assert.Equal(
            (true, true, false, false, false, false),
            (dump.Contains(marker, StringComparison.Ordinal), dump.Contains(Hex("texto-bruto/1/v1"), StringComparison.Ordinal),
             dump.Contains(PseudonymizedTexts.Sanitized, StringComparison.Ordinal), dump.Contains(Hex(PseudonymizedTexts.Sanitized), StringComparison.Ordinal),
             dump.Contains(pseudonymized, StringComparison.Ordinal), dump.Contains(Hex(pseudonymized), StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task MapOfOneDomainDoesNotOpenWithTheKeyOfAnother()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(2);
        var one = await StoredMapAsync(world, 1, await PseudonymizedAsync(world, 1, markers[0]));
        var two = await StoredMapAsync(world, 2, await PseudonymizedAsync(world, 2, markers[1]));

        var opened = world.Content.Open(ContentColumns.Entries, 1, one.Entries, one.Key);

        Assert.Contains(PseudonymizedTexts.OriginalOf(markers[0]), opened, StringComparison.Ordinal);
        Assert.Equal(("mapa-reversao/1/v1", "mapa-reversao/2/v1"), (EnvelopeCipher.LabelOf(one.Entries), EnvelopeCipher.LabelOf(two.Entries)));
        _ = Assert.Throws<EnvelopeRefusedException>(() => world.Content.Open(ContentColumns.Entries, 1, two.Entries, one.Key));
        _ = Assert.Throws<EnvelopeRefusedException>(() => world.Content.Open(ContentColumns.Entries, 2, two.Entries, one.Key));
    }

    [DatabaseFact]
    public async Task EveryMapOpeningAddsAnEventWithoutTheValue()
    {
        await using var world = await ContentWorld.CreateAsync();
        var marker = PseudonymizedTexts.Draw(1)[0];
        var map = (long)(await world.OwnerInDomainAsync(1, MapOfItem, ("id", await PseudonymizedAsync(world, 1, marker))))!;
        var reading = new ContentRead(CustodyAuthor.System, ContentColumns.Entries, 1, map);

        var first = await world.Reader.ReadAsync(reading, Token);
        var second = await world.Reader.ReadAsync(reading, Token);

        var original = PseudonymizedTexts.OriginalOf(marker);
        var events = await world.ScalarAsync(
            "SELECT string_agg(item_kind || ',' || item_id || ',' || step, ';' ORDER BY sequence) FROM public.custody_event WHERE step = 'reversal_entries_read'");
        var rows = (string)(await world.ScalarAsync("SELECT string_agg(row_to_json(e)::text, '' ORDER BY sequence) FROM public.custody_event e"))!;
        Assert.Equal((true, true), (first.Text!.Contains(original, StringComparison.Ordinal), second.Text!.Contains(original, StringComparison.Ordinal)));
        Assert.Equal($"reversal_map,{map},reversal_entries_read;reversal_map,{map},reversal_entries_read", events);
        Assert.DoesNotContain(original, rows, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task MapCannotBeFilteredInTheDatabase()
    {
        await using var world = await ContentWorld.CreateAsync();
        var markers = PseudonymizedTexts.Draw(2);
        _ = await PseudonymizedAsync(world, 1, markers[0]);
        _ = await PseudonymizedAsync(world, 1, markers[1]);
        var original = PseudonymizedTexts.OriginalOf(markers[0]);

        var byBytes = await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.reversal_map WHERE position(convert_to(@original, 'UTF8') IN entries) > 0", ("original", original));
        var byText = await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.reversal_map WHERE encode(entries, 'escape') LIKE '%' || @original || '%'", ("original", original));
        var sealedMaps = await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.reversal_map WHERE entries IS NOT NULL");
        var byField = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.reversal_map WHERE entries ->> 'original' = @original", ("original", original)));

        Assert.Equal((0L, 0L, 2L, UndefinedFunction), ((long)byBytes!, (long)byText!, (long)sealedMaps!, byField.SqlState));
    }

    [DatabaseFact]
    public async Task MapCannotBeOrderedInTheDatabase()
    {
        await using var world = await ContentWorld.CreateAsync();
        foreach (var marker in PseudonymizedTexts.Draw(2))
        {
            _ = await PseudonymizedAsync(world, 1, marker);
        }

        // An order by the column itself compares sealed bytes, which hold no byte of any original; an order by what the entries say is refused, as the bytes are no text nor document.
        var byField = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "SELECT id FROM content.reversal_map ORDER BY entries ->> 'original' LIMIT 1"));
        var byText = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "SELECT id FROM content.reversal_map ORDER BY convert_from(entries, 'UTF8') LIMIT 1"));

        Assert.Equal((UndefinedFunction, NotInRepertoire), (byField.SqlState, byText.SqlState));
    }

    private static async Task<long> PseudonymizedAsync(ContentWorld world, int domainId, string marker)
    {
        var written = await world.RealWriter.AddPseudonymizedItemAsync(domainId, domainId, PseudonymizedTexts.Holding(marker), Token);
        return Assert.NotNull(written.ItemId);
    }

    private static async Task<(byte[] Entries, ContentKey Key)> StoredMapAsync(ContentWorld world, int domainId, long item)
    {
        var entries = (byte[])(await world.InDomainAsync(domainId, "SELECT entries FROM content.reversal_map WHERE item_id = @id", ("id", item)))!;
        var epoch = (int)(await world.InDomainAsync(domainId, "SELECT dek_epoch FROM content.reversal_map WHERE item_id = @id", ("id", item)))!;
        var wrapped = (byte[])(await world.InDomainAsync(domainId, "SELECT dek_wrapped FROM content.reversal_map WHERE item_id = @id", ("id", item)))!;
        return (entries, new ContentKey(epoch, wrapped));
    }

    private static string Hex(string text) => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(text));
}
