using System.Globalization;
using System.Text.Json.Nodes;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>A copy of the data in a folder as the 005 describes it: a manifest and a file of rows for each table, made from a populated database and open to be damaged.</summary>
internal sealed class CopyFolder : IDisposable
{
    public static readonly string[] Tables =
    [
        "public.domain", "public.derivation_label", "public.person", "public.person_credential", "public.person_change", "public.sign_in_attempt",
        "public.custody_event",
        "content.item_text", "content.reversal_map", "content.item_emptied",
    ];

    private static readonly Dictionary<string, string> Keys = new() { ["public.person_credential"] = "person_id", ["public.derivation_label"] = "label", ["public.custody_event"] = "sequence" };

    private CopyFolder(string path) => Path = path;

    public string Path { get; }

    /// <summary>The number of rows the copy holds in all its files.</summary>
    public int RowCount => Tables.Sum(table => File.ReadAllLines(FileOf(table)).Length);

    public static async Task<CopyFolder> OfAsync(FidelityWorld source)
    {
        var copy = new CopyFolder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "zqx-copy-" + Guid.NewGuid().ToString("N")));
        _ = Directory.CreateDirectory(copy.Path);
        foreach (var table in Tables)
        {
            var rows = table.StartsWith("content.", StringComparison.Ordinal) ? await ContentRowsAsync(source, table) : await OwnerRowsAsync(source, table);
            File.WriteAllLines(copy.FileOf(table), JsonNode.Parse(rows)!.AsArray().Select(row => row!.ToJsonString()));
        }

        var version = Assert.Single(await source.OwnerColumnAsync("SELECT max(version)::text FROM public.schema_version"));
        copy.WriteManifest(int.Parse(version, CultureInfo.InvariantCulture), Tables);
        return copy;
    }

    /// <summary>The tip of the chain of the database, as the 005 asks it to be noted outside the copy.</summary>
    public static async Task<(long Sequence, string Digest)> TipOfAsync(FidelityWorld source)
    {
        var tip = Assert.Single(await source.OwnerColumnAsync("SELECT sequence || ':' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1")).Split(':');
        return (long.Parse(tip[0], CultureInfo.InvariantCulture), tip[1]);
    }

    public void WriteManifest(int schemaVersion, IEnumerable<string> tables) =>
        File.WriteAllText(
            System.IO.Path.Combine(Path, "manifest.json"),
            new JsonObject { ["format"] = 1, ["schemaVersion"] = schemaVersion, ["files"] = new JsonArray(tables.Select(table => (JsonNode)(table + ".jsonl")).ToArray()) }.ToJsonString());

    public void Rewrite(string table, Func<List<JsonNode>, IEnumerable<JsonNode>> change) =>
        File.WriteAllLines(FileOf(table), change([.. File.ReadAllLines(FileOf(table)).Select(line => JsonNode.Parse(line)!)]).Select(row => row.ToJsonString()));

    public void Dispose() => Directory.Delete(Path, recursive: true);

    private string FileOf(string table) => System.IO.Path.Combine(Path, table + ".jsonl");

    // nosemgrep: sql-concatenation-csharp interpolates only the key and the table of the copy, literals of this fixture, never a value.
    private static string AllRows(string key, string table) => $"SELECT coalesce(jsonb_agg(to_jsonb(t) ORDER BY {key}), '[]') FROM {table} t";

    private static async Task<string> OwnerRowsAsync(FidelityWorld source, string table) =>
        Assert.Single(await source.OwnerColumnAsync(AllRows(Keys.GetValueOrDefault(table, "id"), table)));

    private static async Task<string> ContentRowsAsync(FidelityWorld source, string table)
    {
        var all = new JsonArray();
        foreach (var domain in new[] { 1, 2 })
        {
            var part = (string)(await source.InDomainAsync(domain, AllRows("id", table)))!;
            foreach (var row in JsonNode.Parse(part)!.AsArray())
            {
                all.Add(row!.DeepClone());
            }
        }

        return all.ToJsonString();
    }
}
