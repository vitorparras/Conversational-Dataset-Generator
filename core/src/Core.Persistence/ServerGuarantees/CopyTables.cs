using System.Data.Common;
using System.Globalization;
using System.Text;
using Core.Persistence.Content;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>The tables of the copy, in the fixed order of the load of the 005, and the writing of each as a file of rows, one on each line, from the snapshot one transaction holds.</summary>
internal static class CopyTables
{
    private const string DomainIds = "SELECT id FROM public.domain ORDER BY id";

    private static readonly IEnumerable<int> NoDomain = Enumerable.Repeat(0, 1);

    /// <summary>Every table with the statement that reads it as the rows the database writes; the content is read one domain at a time, the rest at once.</summary>
    internal static Table[] All { get; } = TablesOf();

    /// <summary>The domains of the snapshot, read once so that every table of the content is read for the same ones.</summary>
    internal static async Task<List<int>> DomainsAsync(NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(DomainIds, transaction.Connection, transaction);
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
            new Table("public.person", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person t ORDER BY id", InDomain: false),
            new Table("public.person_credential", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person_credential t ORDER BY person_id", InDomain: false),
            new Table("public.person_change", "SELECT pg_catalog.to_jsonb(t)::text FROM public.person_change t ORDER BY id", InDomain: false),
            new Table("public.sign_in_attempt", "SELECT pg_catalog.to_jsonb(t)::text FROM public.sign_in_attempt t ORDER BY id", InDomain: false),
            new Table("public.custody_event", "SELECT pg_catalog.to_jsonb(t)::text FROM public.custody_event t ORDER BY sequence", InDomain: false),
            new Table("content.item_text", ContentQueries.ReadCopyItemText, InDomain: true),
            new Table("content.reversal_map", ContentQueries.ReadCopyReversalMap, InDomain: true),
            new Table("content.item_emptied", ContentQueries.ReadCopyItemEmptied, InDomain: true),
        };
        return tables;
    }

    private static async Task DeclareDomainAsync(NpgsqlTransaction transaction, Table table, int domain, CancellationToken cancellationToken)
    {
        if (!table.InDomain)
        {
            return;
        }

        var command = new NpgsqlCommand(ContentTransaction.Declare, transaction.Connection, transaction);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("domain", domain.ToString(CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task WriteLinesAsync(NpgsqlTransaction transaction, string statement, TextWriter writer, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(statement, transaction.Connection, transaction);
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

    /// <summary>A table of the copy: its name, the statement that reads its rows and whether the row policy holds the read to one domain at a time.</summary>
    internal sealed record Table(string Name, string Statement, bool InDomain);

    /// <summary>What one snapshot holds while the tables are written: the folder, the transaction that reads and the domains it saw.</summary>
    internal sealed record Snapshot(string Folder, NpgsqlTransaction Transaction, List<int> Domains);
}
