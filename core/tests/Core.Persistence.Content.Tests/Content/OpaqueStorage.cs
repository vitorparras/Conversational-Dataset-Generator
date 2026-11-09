using System.Text;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// What a copy of the storage shows to whoever has it and not the key: the dump the server writes and the files of the server read as bytes. The protected columns are
/// found in the catalog by their mark, each is seeded through the path of the application with a text of its own, and the detector says which of those texts a copy shows.
/// </summary>
internal static class OpaqueStorage
{
    private const int MinimumRun = 12;
    private const byte LineBreak = 0x0a;
    private const string MarkedColumns =
        "SELECT n.nspname || '.' || c.relname || '.' || a.attname FROM pg_description d "
        + "JOIN pg_class c ON c.oid = d.objoid JOIN pg_namespace n ON n.oid = c.relnamespace "
        + "JOIN pg_attribute a ON a.attrelid = d.objoid AND a.attnum = d.objsubid "
        + "WHERE d.classoid = 'pg_class'::regclass AND d.objsubid > 0 AND d.description LIKE 'protected:%' ORDER BY 1";

    /// <summary>The column of the raw text, as the catalog names it.</summary>
    public const string RawText = "content.item_text.raw_text";

    /// <summary>The column of the sanitized text.</summary>
    public const string SanitizedText = "content.item_text.sanitized_text";

    /// <summary>The column of the occurrences found in the item and the destination of each.</summary>
    public const string Occurrences = "content.item_text.occurrences";

    /// <summary>The column of the entries of the reversal map.</summary>
    public const string Entries = "content.reversal_map.entries";

    /// <summary>The column of the key of the item, wrapped: what it protects is the key in clear, which no copy of the storage may show.</summary>
    public const string ItemKey = "content.item_text.dek_wrapped";

    /// <summary>The column of the key of the map, wrapped.</summary>
    public const string MapKey = "content.reversal_map.dek_wrapped";

    private static readonly string[] Seedable = [RawText, SanitizedText, Occurrences, Entries];

    private static readonly string[] KeyColumns = [ItemKey, MapKey];

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
        string[] known = [.. Seedable, .. KeyColumns];
        Assert.Empty(marked.Except(known, StringComparer.Ordinal));
        Assert.Empty(known.Except(marked, StringComparer.Ordinal));
        var texts = Seedable.ToDictionary(column => column, column => $"zqx-{Guid.NewGuid():N}", StringComparer.Ordinal);
        var pseudonymized = $"zqx-{Guid.NewGuid():N}";
        ItemOccurrence[] occurrences = [new(0, 1, texts[Occurrences], OccurrenceDestination.Remove)];
        var written = new NewItemText(texts[RawText], texts[SanitizedText], pseudonymized, occurrences);
        var item = await world.Writer.AddItemTextAsync(1, written, TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, texts[Entries], TestContext.Current.CancellationToken);
        await AssertEveryMarkedColumnHoldsSomethingAsync(world, marked);
        return new Material(texts, pseudonymized) { Keys = await KeysInClearAsync(world) };
    }

    /// <summary>
    /// The positive control of the seeding: a marked column the writer did not fill would be read as hidden by a search that had nothing to find in it,
    /// so each marked column must hold a value, and the one that does not fails the test by name.
    /// </summary>
    private static async Task AssertEveryMarkedColumnHoldsSomethingAsync(ContentWorld world, IReadOnlyList<string> marked)
    {
        var empty = new List<string>();
        foreach (var column in marked)
        {
            var parts = column.Split('.');
            // nosemgrep: sql-concatenation-csharp interpolates the names of a column read from the catalog of a throwaway database, never input.
            var filled = await world.OwnerInDomainAsync(1, $"SELECT count(*) FROM {parts[0]}.{parts[1]} WHERE {parts[2]} IS NOT NULL");
            if ((long)filled! == 0)
            {
                empty.Add(column);
            }
        }

        Assert.Empty(empty);
    }

    /// <summary>The dump of the database and the places in the files of the server where any of the texts or of the extra needles are, with the files forced to disk first.</summary>
    public static Task<Observation> ObserveAsync(ContentWorld world, Material material, params byte[][] extraNeedles)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(material);
        var texts = material.Texts.Values.Append(material.Pseudonymized);
        return ObserveNeedlesAsync(world, material, [.. texts.Select(Encoding.UTF8.GetBytes), .. extraNeedles]);
    }

    /// <summary>The key of the row of each table in clear, unwrapped the way the path of the application does: this is what a copy of the storage must never show.</summary>
    private static async Task<IReadOnlyDictionary<string, byte[]>> KeysInClearAsync(ContentWorld world)
    {
        var keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var column in KeyColumns)
        {
            var table = column[..column.LastIndexOf('.')];
            // nosemgrep: sql-concatenation-csharp interpolates the name of a table fixed above, never input.
            var epoch = (int)(await world.OwnerInDomainAsync(1, $"SELECT dek_epoch FROM {table} ORDER BY id DESC LIMIT 1"))!;
            // nosemgrep: sql-concatenation-csharp interpolates the name of a table fixed above, never input.
            var wrapped = (byte[])(await world.OwnerInDomainAsync(1, $"SELECT dek_wrapped FROM {table} ORDER BY id DESC LIMIT 1"))!;
            using var key = world.Keys.Unwrap(epoch, wrapped);
            keys[column] = key.AsSpan().ToArray();
        }

        return keys;
    }

    private static async Task<Observation> ObserveNeedlesAsync(ContentWorld world, Material material, byte[][] needles)
    {
        var fixture = await PostgresFixture.SharedAsync();
        var files = await fixture.ServerFilesHoldingAsync(needles);
        var keysInFiles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (column, key) in material.Keys)
        {
            if ((await fixture.ServerFilesHoldingAsync(KeyNeedles(key))).Count > 0)
            {
                _ = keysInFiles.Add(column);
            }
        }

        return new Observation(await world.Database.DumpAsync(), files) { KeysInFiles = keysInFiles };
    }

    /// <summary>
    /// What the search of the files takes for a key, one needle at a time so that a match names its column: the bytes raw, as the server writes a bytea into its files,
    /// and the two ways of writing them as hex. The search reads line by line, so a key that holds a line break is searched by its longest run without one,
    /// which no other key shares; a key too broken to leave a run of twelve bytes stops the test.
    /// </summary>
    private static byte[][] KeyNeedles(byte[] key)
    {
        var run = LongestRunWithoutLineBreak(key);
        return run.Length < MinimumRun
            ? throw new InvalidOperationException("The key holds line breaks that leave no run the search of the files could match.")
            : [run, Encoding.ASCII.GetBytes(Convert.ToHexStringLower(key)), Encoding.ASCII.GetBytes(Convert.ToHexString(key))];
    }

    private static byte[] LongestRunWithoutLineBreak(byte[] key)
    {
        var best = (Start: 0, Length: 0);
        var start = 0;
        for (var index = 0; index <= key.Length; index++)
        {
            if (index == key.Length || key[index] == LineBreak)
            {
                best = index - start > best.Length ? (start, index - start) : best;
                start = index + 1;
            }
        }

        return key[best.Start..(best.Start + best.Length)];
    }

    /// <summary>The columns, among those given, whose text the observation shows, in the dump as text or as the hex a bytea is dumped in, or in any file of the server.</summary>
    public static IReadOnlyList<string> ColumnsShown(Observation observation, Material material, params string[] columns)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(material);
        return [.. columns.Where(column => ShownBy(observation, material, column)).Order(StringComparer.Ordinal)];
    }

    private static bool ShownBy(Observation observation, Material material, string column) =>
        material.Keys.TryGetValue(column, out var key)
            ? observation.Dump.Contains(Convert.ToHexStringLower(key), StringComparison.Ordinal) || observation.KeysInFiles.Contains(column)
            : observation.ShowsInDump(material.Texts[column]) || observation.ShowsInFiles(material.Texts[column]);

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
    internal sealed record Material(IReadOnlyDictionary<string, string> Texts, string Pseudonymized)
    {
        /// <summary>The key of each row in clear, by the column that wraps it.</summary>
        public IReadOnlyDictionary<string, byte[]> Keys { get; init; } = new Dictionary<string, byte[]>();

        /// <summary>Every marked column the seeding filled.</summary>
        public IEnumerable<string> Columns => Texts.Keys.Concat(Keys.Keys);

        /// <summary>What the column would hold if it were written in clear.</summary>
        public byte[] InClear(string column) => Keys.TryGetValue(column, out var key) ? key : Encoding.UTF8.GetBytes(Texts[column]);
    }

    /// <summary>What the copy of the storage shows: the dump of the database and the matches in the files of the server.</summary>
    /// <param name="Dump">What pg_dump wrote, with the data as inserts.</param>
    /// <param name="Files">Where the files of the server hold any needle, and what matched.</param>
    internal sealed record Observation(string Dump, IReadOnlyList<PostgresFixture.FileMatch> Files)
    {
        /// <summary>The columns whose key the search of the files found, raw or as hex.</summary>
        public IReadOnlySet<string> KeysInFiles { get; init; } = new HashSet<string>();

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
