using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// The copy of the data as the 005 describes it: a folder with a manifest, a file of rows for each table in the fixed order of the load, and the configuration as it may be shown. The folder is made
/// only of those: the rows the database writes, with the sealed columns as the bytes they are and never opened, and the configuration with its sensitive values already redacted, so the key and the
/// files of the custody have no way in. The seal of the chain is recorded first and goes out in the answer, not into the folder.
/// </summary>
public sealed class DataCopy
{
    private const int CopyFormat = 1;
    private const string ManifestFile = "manifest.json";
    private const string ConfigurationFile = "configuration.json";
    private const string ReadOnly = "SET TRANSACTION READ ONLY";
    private const string ReadSchemaVersion = "SELECT coalesce(pg_catalog.max(version), 0) FROM public.schema_version";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    private readonly NpgsqlDataSource dataSource;
    private readonly TimeProvider time;

    /// <summary>The copy reads, and records the seal, through the data source built with the credential of the owner, which the row policy of the content holds to a domain at a time.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="time">Tells the moment of the seal.</param>
    public DataCopy(NpgsqlDataSource dataSource, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        this.time = time;
    }

    /// <summary>The moment each table was written, which a test uses to write to the database in the middle of the copy; the copy ignores what the hook does.</summary>
    internal Func<string, Task>? AfterTable { get; init; }

    /// <summary>Makes the folder, records the seal, then writes all the tables from one snapshot; a folder that holds anything, or a seal the chain did not take, leaves nothing written.</summary>
    /// <param name="folder">The folder of the copy: it does not exist yet or is empty.</param>
    /// <param name="configuration">The configuration in force as it may be shown, sensitive values redacted.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    public Task<DataCopyResult> WriteAsync(string folder, IReadOnlyList<EffectiveEntry> configuration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(configuration);
        return WritingAsync(folder, configuration, cancellationToken);
    }

    private async Task<DataCopyResult> WritingAsync(string folder, IReadOnlyList<EffectiveEntry> configuration, CancellationToken cancellationToken)
    {
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            return new DataCopyResult(DataCopyOutcome.FolderNotEmpty, Seal: null);
        }

        _ = Directory.CreateDirectory(folder);
        var sealing = await new ChainSealer(dataSource, time).RecordAsync(CustodyAuthor.System, cancellationToken).ConfigureAwait(false);
        if (sealing.Seal is null)
        {
            return new DataCopyResult(DataCopyOutcome.SealNotRecorded, Seal: null);
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                await WriteSnapshotAsync(folder, configuration, transaction, cancellationToken).ConfigureAwait(false);
            }
        }

        return new DataCopyResult(DataCopyOutcome.Made, sealing.Seal);
    }

    private async Task WriteSnapshotAsync(string folder, IReadOnlyList<EffectiveEntry> configuration, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        _ = await ScalarAsync(transaction, ReadOnly, cancellationToken).ConfigureAwait(false);
        var snapshot = new CopyTables.Snapshot(folder, transaction, await CopyTables.DomainsAsync(transaction, cancellationToken).ConfigureAwait(false));
        foreach (var table in CopyTables.All)
        {
            await CopyTables.WriteAsync(table, snapshot, cancellationToken).ConfigureAwait(false);
            if (AfterTable is { } hook)
            {
                await hook(table.Name).ConfigureAwait(false);
            }
        }

        await File.WriteAllTextAsync(Path.Combine(folder, ConfigurationFile), JsonSerializer.Serialize(configuration, Json), cancellationToken).ConfigureAwait(false);
        var manifest = await ManifestAsync(transaction, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(folder, ManifestFile), manifest, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<object?> ScalarAsync(NpgsqlTransaction transaction, string statement, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(statement, transaction.Connection, transaction);
        await using (command.ConfigureAwait(false))
        {
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<string> ManifestAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var version = Convert.ToInt32(await ScalarAsync(transaction, ReadSchemaVersion, cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        return JsonSerializer.Serialize(new Manifest(CopyFormat, version, Array.ConvertAll(CopyTables.All, table => table.Name + ".jsonl"), ConfigurationFile), Json);
    }

    private sealed record Manifest(int Format, int SchemaVersion, IReadOnlyList<string> Files, string Configuration);
}
