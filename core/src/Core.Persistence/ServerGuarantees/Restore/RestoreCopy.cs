using System.Security.Cryptography;
using System.Text.Json;

namespace Core.Persistence.ServerGuarantees.Restore;

/// <summary>A copy of the data as the 005 describes it: a folder with a manifest and, for each table, a file with one row on each line.</summary>
public sealed class RestoreCopy
{
    private const int Format = 1;
    private const int BatchSize = 500;
    private readonly string folder;
    private readonly IReadOnlyList<string> files;

    private RestoreCopy(string folder, int schemaVersion, string name, IReadOnlyList<string> files)
    {
        this.folder = folder;
        SchemaVersion = schemaVersion;
        Name = name;
        this.files = files;
    }

    /// <summary>The version of the schema the copy came from.</summary>
    public int SchemaVersion { get; }

    /// <summary>The SHA-256 of the manifest in lowercase hexadecimal, which names the copy in the chain.</summary>
    public string Name { get; }

    /// <summary>Reads the manifest of the copy; a manifest this build does not read is an <see cref="InvalidDataException" />.</summary>
    public static RestoreCopy Open(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var bytes = File.ReadAllBytes(Path.Combine(folder, "manifest.json"));
        try
        {
            using var manifest = JsonDocument.Parse(bytes);
            var root = manifest.RootElement;
            var files = root.GetProperty("files").EnumerateArray().Select(file => file.GetString() ?? string.Empty).ToList();
            return root.GetProperty("format").GetInt32() == Format
                ? new RestoreCopy(folder, root.GetProperty("schemaVersion").GetInt32(), Convert.ToHexStringLower(SHA256.HashData(bytes)), files)
                : throw new InvalidDataException("The format of the copy is not the one this build reads.");
        }
        catch (Exception unreadable) when (unreadable is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("The manifest of the copy is not one this build reads.", unreadable);
        }
    }

    /// <summary>Whether the manifest lists the file of the table.</summary>
    public bool Has(string table) => files.Contains(table + ".jsonl");

    /// <summary>The rows of the table, as the arrays of JSON that one call of the load takes; none for a table with no row.</summary>
    public IEnumerable<string> BatchesOf(string table) =>
        Has(table)
            ? File.ReadLines(Path.Combine(folder, table + ".jsonl")).Where(line => line.Length > 0).Chunk(BatchSize).Select(rows => "[" + string.Join(',', rows) + "]")
            : throw new InvalidDataException("The copy has no file for the table.");
}
