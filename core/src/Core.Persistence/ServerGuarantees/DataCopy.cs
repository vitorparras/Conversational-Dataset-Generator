using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Persistence.Content;
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
    private const int Format = 1;
    private const string ManifestFile = "manifest.json";
    private const string ConfigurationFile = "configuration.json";
    private const string DomainIds = "SELECT id FROM public.domain ORDER BY id";
    private const string SchemaVersion = "SELECT coalesce(pg_catalog.max(version), 0) FROM public.schema_version";

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    private static readonly Table[] Tables = new[]
    {
        new Table("public.domain", "SELECT pg_catalog.to_jsonb(t)::text FROM public.domain t ORDER BY id", InDomain: false),
        new Table("public.derivation_label", "SELECT pg_catalog.to_jsonb(t)::text FROM public.derivation_label t ORDER BY label", InDomain: false),
        new Table("public.person", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person t ORDER BY id", InDomain: false),
        new Table("public.person_credential", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person_credential t ORDER BY person_id", InDomain: false),
        new Table("public.person_change", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person_change t ORDER BY id", InDomain: false),
        new Table("public.custody_event", "SELECT pg_catalog.to_jsonb(t)::text FROM public.custody_event t ORDER BY sequence", InDomain: false),
        new Table("content.item_text", ContentQueries.ReadCopyItemText, InDomain: true),
        new Table("content.reversal_map", ContentQueries.ReadCopyReversalMap, InDomain: true),
        new Table("content.item_emptied", ContentQueries.ReadCopyItemEmptied, InDomain: true),
    };

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

    /// <summary>Records the seal, then writes the folder; a folder that holds anything, or a seal the chain did not take, leaves nothing written.</summary>
    /// <param name="folder">The folder of the copy: it does not exist yet or is empty.</param>
    /// <param name="configuration">The configuration in force as it may be shown, sensitive values redacted.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    public async Task<DataCopyResult> WriteAsync(string folder, IReadOnlyList<EffectiveEntry> configuration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        ArgumentNullException.ThrowIfNull(configuration);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            return new DataCopyResult(DataCopyOutcome.FolderNotEmpty, Seal: null);
        }

        var sealing = await new ChainSealer(dataSource, time).RecordAsync(CustodyAuthor.System, cancellationToken).ConfigureAwait(false);
        if (sealing.Seal is null)
        {
            return new DataCopyResult(DataCopyOutcome.SealNotRecorded, Seal: null);
        }

        _ = Directory.CreateDirectory(folder);
        foreach (var table in Tables)
        {
            await WriteTableAsync(folder, table, cancellationToken).ConfigureAwait(false);
        }

        await File.WriteAllTextAsync(Path.Combine(folder, ConfigurationFile), JsonSerializer.Serialize(configuration, Json), cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(folder, ManifestFile), await ManifestAsync(cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return new DataCopyResult(DataCopyOutcome.Made, sealing.Seal);
    }

    private async Task<string> ManifestAsync(CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(SchemaVersion);
        await using (command.ConfigureAwait(false))
        {
            var version = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            return JsonSerializer.Serialize(new Manifest(Format, version, Tables.Select(table => table.Name + ".jsonl").ToList(), ConfigurationFile), Json);
        }
    }

    private async Task WriteTableAsync(string folder, Table table, CancellationToken cancellationToken)
    {
        var writer = new StreamWriter(Path.Combine(folder, table.Name + ".jsonl"), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" };
        await using (writer.ConfigureAwait(false))
        {
            if (!table.InDomain)
            {
                await WriteRowsAsync(table.Statement, writer, cancellationToken).ConfigureAwait(false);
                return;
            }

            foreach (var domain in await DomainsAsync(cancellationToken).ConfigureAwait(false))
            {
                await WriteDomainRowsAsync(table.Statement, domain, writer, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task WriteDomainRowsAsync(string statement, int domain, StreamWriter writer, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, domain, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = transaction.CreateCommand(statement);
            await using (command.ConfigureAwait(false))
            {
                await WriteLinesAsync(command, writer, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task WriteRowsAsync(string statement, StreamWriter writer, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            await WriteLinesAsync(command, writer, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteLinesAsync(NpgsqlCommand command, StreamWriter writer, CancellationToken cancellationToken)
    {
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await writer.WriteLineAsync(reader.GetString(0).AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<List<int>> DomainsAsync(CancellationToken cancellationToken)
    {
        var domains = new List<int>();
        var command = dataSource.CreateCommand(DomainIds);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    domains.Add(reader.GetInt32(0));
                }
            }
        }

        return domains;
    }

    private sealed record Table(string Name, string Statement, bool InDomain);

    private sealed record Manifest(int Format, int SchemaVersion, IReadOnlyList<string> Files, string Configuration);
}
