using System.Data.Common;
using System.Text;
using Core.Persistence.Content;

namespace Core.Persistence.ServerGuarantees;

/// <summary>The tables of the copy, in the fixed order of the load of the 005, and the writing of each as a file of rows, one on each line, from the snapshot one transaction holds.</summary>
internal static class CopyTables
{
    private const string DomainIds = "SELECT id FROM public.domain ORDER BY id";

    private static readonly IEnumerable<int> NoDomain = Enumerable.Repeat(0, 1);

    internal static Table[] All { get; } = TablesOf();

    internal static async Task<List<int>> DomainsAsync(ContentTransaction transaction, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(DomainIds);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await CollectDomainsAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Writes the file of the table in the folder, reading it through the transaction of the snapshot.</summary>
    internal static async Task WriteAsync(Table table, Snapshot snapshot, CancellationToken cancellationToken)
    {
        var writer = new StreamWriter(Path.Combine(snapshot.Folder, table.Name + ".jsonl"), append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) { NewLine = "\n" };
        await using (writer.ConfigureAwait(false))
        {
            foreach (var domain in table.InDomain ? snapshot.Domains : NoDomain)
            {
                await DeclareDomainAsync(snapshot.Transaction, table, domain, cancellationToken).ConfigureAwait(false);
                await WriteLinesAsync(snapshot.Transaction, table.Statement, writer, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static Table[] TablesOf()
    {
        var tables = new[]
        {
            new Table("public.domain", "SELECT pg_catalog.to_jsonb(t)::text FROM public.domain t ORDER BY id", InDomain: false),
            new Table("public.derivation_label", "SELECT pg_catalog.to_jsonb(t)::text FROM public.derivation_label t ORDER BY label", InDomain: false),
            new Table("public.derivation_label_revocation", "SELECT pg_catalog.to_jsonb(t)::text FROM public.derivation_label_revocation t ORDER BY label", InDomain: false),
            new Table("public.person", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person t ORDER BY id", InDomain: false),
            new Table("public.person_credential", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person_credential t ORDER BY person_id", InDomain: false),
            new Table("public.person_change", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person_change t ORDER BY id", InDomain: false),
            new Table("public.sign_in_attempt", "SELECT pg_catalog.to_jsonb(t)::text FROM public.sign_in_attempt t ORDER BY id", InDomain: false),
            new Table("public.custody_event", "SELECT pg_catalog.to_jsonb(t)::text FROM public.custody_event t ORDER BY sequence", InDomain: false),
            new Table("content.ingestion_batch", ContentQueries.ReadCopyIngestionBatch, InDomain: true),
            new Table("content.item_text", ContentQueries.ReadCopyItemText, InDomain: true),
            new Table("content.reversal_map", ContentQueries.ReadCopyReversalMap, InDomain: true),
            new Table("content.item_emptied", ContentQueries.ReadCopyItemEmptied, InDomain: true),
            new Table("content.replication_occurrence", ContentQueries.ReadCopyReplicationOccurrence, InDomain: true),
            new Table("content.replication_mark", ContentQueries.ReadCopyReplicationMark, InDomain: true),
        };
        return tables;
    }

    private static Task DeclareDomainAsync(ContentTransaction transaction, Table table, int domain, CancellationToken cancellationToken) =>
        table.InDomain ? transaction.ChangeDomainAsync(domain, cancellationToken) : Task.CompletedTask;

    private static async Task WriteLinesAsync(ContentTransaction transaction, string statement, TextWriter writer, CancellationToken cancellationToken)
    {
        var command = transaction.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                await CopyLinesAsync(reader, writer, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task CopyLinesAsync(DbDataReader reader, TextWriter writer, CancellationToken cancellationToken)
    {
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            await writer.WriteLineAsync(reader.GetString(0).AsMemory(), cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<List<int>> CollectDomainsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var domains = new List<int>();
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            domains.Add(reader.GetInt32(0));
        }

        return domains;
    }

    internal sealed record Table(string Name, string Statement, bool InDomain);

    internal sealed record Snapshot(string Folder, ContentTransaction Transaction, List<int> Domains);
}
