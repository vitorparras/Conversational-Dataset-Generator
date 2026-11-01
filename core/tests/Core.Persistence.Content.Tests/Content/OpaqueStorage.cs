using System.Text;
using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// What a copy of the storage shows to whoever has it and not the key: the dump the server writes and the files of the server read as bytes. The protected columns are
/// found in the catalog by their mark, each is seeded through the path of the application with a text of its own, and the detector says which of those texts a copy shows.
/// </summary>
internal static class OpaqueStorage
{
    private const string MarkedColumns =
        "SELECT n.nspname || '.' || c.relname || '.' || a.attname FROM pg_description d "
        + "JOIN pg_class c ON c.oid = d.objoid JOIN pg_namespace n ON n.oid = c.relnamespace "
        + "JOIN pg_attribute a ON a.attrelid = d.objoid AND a.attnum = d.objsubid "
        + "WHERE d.classoid = 'pg_class'::regclass AND d.objsubid > 0 AND d.description LIKE 'protected:%' ORDER BY 1";

    /// <summary>The column of the raw text, as the catalog names it.</summary>
    public const string RawText = "content.item_text.raw_text";

    /// <summary>The column of the sanitized text.</summary>
    public const string SanitizedText = "content.item_text.sanitized_text";

    /// <summary>The column of the entries of the reversal map.</summary>
    public const string Entries = "content.reversal_map.entries";

    private static readonly string[] Seedable = [RawText, SanitizedText, Entries];

    /// <summary>The columns the catalog marks as protected, as schema.table.column; the marks are the ones the migrations write.</summary>
    public static Task<IReadOnlyList<string>> MarkedColumnsAsync(ContentWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return ReadMarkedColumnsAsync(world.Database.OwnerConnectionString);
    }

    private static async Task<IReadOnlyList<string>> ReadMarkedColumnsAsync(string connectionString)
    {
        await using var connection = await Sql.OpenAsync(connectionString);
        await using var command = new NpgsqlCommand(MarkedColumns, connection);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    /// <summary>
    /// Writes one text of its own into every marked column and one into the column left in clear, all through the writer of the application. A marked column the writer
    /// cannot seed fails the test by name, as does a column the writer seeds that the catalog does not mark: neither is skipped.
    /// </summary>
    public static Task<Material> SeedAsync(ContentWorld world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return SeedMarkedAsync(world);
    }

    private static async Task<Material> SeedMarkedAsync(ContentWorld world)
    {
        var marked = await MarkedColumnsAsync(world);
        Assert.Empty(marked.Except(Seedable, StringComparer.Ordinal));
        Assert.Empty(Seedable.Except(marked, StringComparer.Ordinal));
        var texts = marked.ToDictionary(column => column, column => $"zqx-{Guid.NewGuid():N}", StringComparer.Ordinal);
        var pseudonymized = $"zqx-{Guid.NewGuid():N}";
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText(texts[RawText], texts[SanitizedText], pseudonymized), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, texts[Entries], TestContext.Current.CancellationToken);
        return new Material(texts, pseudonymized);
    }

    /// <summary>The dump of the database and the places in the files of the server where any of the texts or of the extra needles are, with the files forced to disk first.</summary>
    public static Task<Observation> ObserveAsync(ContentWorld world, Material material, params byte[][] extraNeedles)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(material);
        var texts = material.Texts.Values.Append(material.Pseudonymized);
        return ObserveNeedlesAsync(world, [.. texts.Select(Encoding.UTF8.GetBytes), .. extraNeedles]);
    }

    private static async Task<Observation> ObserveNeedlesAsync(ContentWorld world, byte[][] needles)
    {
        var fixture = await PostgresFixture.SharedAsync();
        return new Observation(await world.Database.DumpAsync(), await fixture.ServerFilesHoldingAsync(needles));
    }

    /// <summary>The columns, among those given, whose text the observation shows, in the dump as text or as the hex a bytea is dumped in, or in any file of the server.</summary>
    public static IReadOnlyList<string> ColumnsShown(Observation observation, Material material, params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(material);
        return [.. columns.Where(column => observation.ShowsInDump(material.Texts[column]) || observation.ShowsInFiles(material.Texts[column])).Order(StringComparer.Ordinal)];
    }

    /// <summary>The synthetic key in the ways a dump or a record writes bytes: hex in both cases, with dashes, and base64 with or without padding and in its URL form.</summary>
    public static IReadOnlyList<string> KeyAsText()
    {
        var key = SyntheticCustody.KeyBytes();
        var base64 = Convert.ToBase64String(key);
        return
        [
            Convert.ToHexString(key),
            Convert.ToHexStringLower(key),
            BitConverter.ToString(key),
            base64,
            base64.TrimEnd('='),
            base64.TrimEnd('=').Replace('+', '-').Replace('/', '_'),
        ];
    }

    /// <summary>
    /// The synthetic key as the search of the files takes it: raw, and each of its written forms. The search reads line by line, so a key with a line break could not be found
    /// whole, which is a reason to stop and not to search for less.
    /// </summary>
    public static IReadOnlyList<byte[]> KeyAsNeedles()
    {
        var key = SyntheticCustody.KeyBytes();
        return key.Contains((byte)'\n')
            ? throw new InvalidOperationException("The synthetic key holds a line break, which the search of the files cannot match.")
            : [key, .. KeyAsText().Select(Encoding.UTF8.GetBytes)];
    }

    /// <summary>The texts seeded: one per marked column, by the name of the column, and the one left in clear.</summary>
    /// <param name="Texts">The text of each protected column.</param>
    /// <param name="Pseudonymized">The text of the column kept in clear on purpose.</param>
    internal sealed record Material(IReadOnlyDictionary<string, string> Texts, string Pseudonymized);

    /// <summary>What the copy of the storage shows: the dump of the database and the matches in the files of the server.</summary>
    /// <param name="Dump">What pg_dump wrote, with the data as inserts.</param>
    /// <param name="Files">Where the files of the server hold any needle, and what matched.</param>
    internal sealed record Observation(string Dump, IReadOnlyList<PostgresFixture.FileMatch> Files)
    {
        /// <summary>Whether the dump holds the text, written as text or as the hex of its bytes, which is how a bytea column is dumped.</summary>
        public bool ShowsInDump(string text) =>
            Dump.Contains(text, StringComparison.Ordinal) || Dump.Contains(Convert.ToHexStringLower(Encoding.UTF8.GetBytes(text)), StringComparison.Ordinal);

        /// <summary>Whether any file of the server holds the text.</summary>
        public bool ShowsInFiles(string text) => Files.Any(match => string.Equals(match.Text, text, StringComparison.Ordinal));

        /// <summary>The matches in the files that are none of the texts seeded: what a search for the key found, if it found anything.</summary>
        public IEnumerable<PostgresFixture.FileMatch> FilesBeyond(Material material) =>
            Files.Where(match => !string.Equals(match.Text, material.Pseudonymized, StringComparison.Ordinal) && !material.Texts.Values.Contains(match.Text, StringComparer.Ordinal));

        /// <summary>Whether the dump holds any of the forms.</summary>
        public bool DumpShowsAny(IEnumerable<string> forms) => forms.Any(form => Dump.Contains(form, StringComparison.OrdinalIgnoreCase));
    }
}
