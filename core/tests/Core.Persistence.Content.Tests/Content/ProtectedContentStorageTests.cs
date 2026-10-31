using System.Text;
using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>What the database holds of protected content: bytes that mean nothing without the key, under the label of the purpose and the domain, and the pseudonymized text in clear.</summary>
[Trait("Category", "Database")]
public sealed class ProtectedContentStorageTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Sanitized = "texto sanitizado sintetico zqx";
    private const string Pseudonymized = "texto pseudonimizado sintetico zqx";
    private const string Entries = "mapa de reversao sintetico zqx";

    [DatabaseFact]
    public async Task DumpOfTheDatabaseHoldsNeitherTheRawNorTheSanitizedNorTheMapButKeepsThePseudonymizedText()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw, Sanitized, Pseudonymized), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, Entries, TestContext.Current.CancellationToken);

        var dump = await world.Database.DumpAsync();

        Assert.Equal(
            [false, false, false, true],
            [Holds(dump, Raw), Holds(dump, Sanitized), Holds(dump, Entries), Holds(dump, Pseudonymized)]);
    }

    [DatabaseFact]
    public async Task RawAndSanitizedTextAreStoredUnderTheSameLabelOfTheDomain()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(2, new NewItemText(Raw, Sanitized), TestContext.Current.CancellationToken);

        var labels = new[]
        {
            EnvelopeCipher.LabelOf(await world.StoredAsync(2, ContentQueries.ReadRawText, item)),
            EnvelopeCipher.LabelOf(await world.StoredAsync(2, ContentQueries.ReadSanitizedText, item)),
        };

        Assert.Equal(["texto-bruto/2/v1", "texto-bruto/2/v1"], labels);
    }

    [DatabaseFact]
    public async Task MapIsStoredUnderTheLabelOfItsOwnPurpose()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(1, item, Entries, TestContext.Current.CancellationToken);

        var stored = await world.StoredAsync(1, ContentQueries.ReadReversalEntries, map);

        Assert.Equal("mapa-reversao/1/v1", EnvelopeCipher.LabelOf(stored));
    }

    [DatabaseFact]
    public async Task StoredBytesCarryNoTraceOfTheText()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var stored = await world.StoredAsync(1, ContentQueries.ReadRawText, item);

        Assert.DoesNotContain(Raw, Encoding.Latin1.GetString(stored), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ItemWithoutSanitizedOrPseudonymizedTextKeepsNothingInThoseColumns()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var empty = await world.InDomainAsync(1, "SELECT sanitized_text IS NULL AND pseudonymized_text IS NULL FROM content.item_text WHERE id = @id", ("id", item));

        Assert.True((bool)empty!);
    }

    [DatabaseFact]
    public async Task MapOfAnItemOfAnotherDomainIsRefusedByTheDatabase()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(Raw), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.Writer.AddReversalMapAsync(2, item, Entries, TestContext.Current.CancellationToken));

        Assert.Equal("reversal_map_item_fkey", refused.ConstraintName);
    }

    [Fact]
    public async Task WritingWithNothingToWriteIsRefusedBeforeAnyConnectionOpens()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        using var custody = SyntheticCustody.Holding("texto-bruto/1/v1", "mapa-reversao/1/v1");
        var writer = new ContentWriter(dataSource, custody.Content);

        Assert.Equal(
            ["item", "entries"],
            [
                (await Assert.ThrowsAsync<ArgumentNullException>(() => writer.AddItemTextAsync(1, null!, TestContext.Current.CancellationToken))).ParamName,
                (await Assert.ThrowsAsync<ArgumentNullException>(() => writer.AddReversalMapAsync(1, 1, null!, TestContext.Current.CancellationToken))).ParamName,
            ]);
    }

    private static bool Holds(string dump, string text) => dump.Contains(text, StringComparison.Ordinal);
}
