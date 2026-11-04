using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.ServerGuarantees.Restore;

/// <summary>Puts a copy back in a new database with the application's credential, by the functions of the 0027 only (#64, CA-5), confers the chain and the guarantees, records it.</summary>
public sealed class RestoreLoader
{
    private const string Chain = "public.custody_event";
    private const string Tip = "SELECT sequence || ':' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";
    private const string Version = "SELECT coalesce(pg_catalog.max(version), 0) FROM public.schema_version";

    /// <summary>The tables in the order they come back: the keys, the people and their sign-in attempts, the chain, then the content (the maps after the texts, the emptied after both).</summary>
    private static readonly ImmutableArray<string> Tables = InOrder();

    private readonly NpgsqlDataSource dataSource;
    private readonly GuaranteeVerifier guarantees;
    private readonly TimeProvider time;

    private static ImmutableArray<string> InOrder()
    {
        var tables = ImmutableArray.CreateBuilder<string>();
        tables.AddRange("public.domain", "public.derivation_label", "public.person", "public.person_credential", "public.person_change", "public.sign_in_attempt");
        tables.AddRange(Chain, "content.item_text", "content.reversal_map", "content.item_emptied");
        return tables.ToImmutable();
    }

    /// <summary>The load runs through the data source built with the application's credential and confers the guarantees with the verifier it is given.</summary>
    public RestoreLoader(NpgsqlDataSource dataSource, GuaranteeVerifier guarantees, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(guarantees);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        this.guarantees = guarantees;
        this.time = time;
    }

    /// <summary>Loads the copy and concludes; it never repairs anything.</summary>
    public async Task<RestoreResult> LoadAsync(RestoreCopy copy, RestoreTip tip, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(tip);
        var version = Convert.ToInt32(await ScalarAsync(Version, cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        if (copy.SchemaVersion != version || !Tables.All(copy.Has))
        {
            return new RestoreResult(RestoreOutcome.CopyRefused, $"a cópia: versão {copy.SchemaVersion}, {Tables.Count(copy.Has)} das {Tables.Length} tabelas; o banco: versão {version}");
        }

        if (await RefusalOfOpeningAsync(cancellationToken).ConfigureAwait(false) is { } refusal)
        {
            return new RestoreResult(RestoreOutcome.NotEmpty, refusal);
        }

        var (rows, refused) = await PutBackAsync(copy, cancellationToken).ConfigureAwait(false);
        if (refused is not null)
        {
            return refused;
        }

        var last = await LastEventAsync(cancellationToken).ConfigureAwait(false);
        var matches = tip.Matches(last);
        _ = await CloseAsync(matches, cancellationToken).ConfigureAwait(false);
        return matches
            ? await ConferAsync(copy, tip, rows, cancellationToken).ConfigureAwait(false)
            : new RestoreResult(RestoreOutcome.TipDiffers, $"o último evento da cópia é o {last?.Sequence ?? 0}:{last?.Digest} e o topo anotado fora é o {tip.Sequence}:{tip.Digest}");
    }

    private async Task<(long Rows, RestoreResult? Refused)> PutBackAsync(RestoreCopy copy, CancellationToken cancellationToken)
    {
        var current = new StrongBox<string>(string.Empty);
        try
        {
            return await LoadTablesAsync(copy, current, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception unreadable) when (unreadable is IOException or UnauthorizedAccessException or NpgsqlException)
        {
            _ = await CloseAsync(approved: false, cancellationToken).ConfigureAwait(false);
            return (0, new RestoreResult(RestoreOutcome.CopyRefused, $"a tabela {current.Value} não pôde ser carregada ({unreadable.GetType().Name})"));
        }
    }

    private async Task<(long Rows, RestoreResult? Refused)> LoadTablesAsync(RestoreCopy copy, StrongBox<string> current, CancellationToken cancellationToken)
    {
        long rows = 0;
        var batches = Tables.SelectMany(table =>
        {
            current.Value = table;
            return copy.BatchesOf(table).Select(batch => (Table: table, Batch: batch));
        });
        foreach (var (table, batch) in batches)
        {
            var answer = await LoadBatchAsync(table, batch, cancellationToken).ConfigureAwait(false);
            if (answer < 0)
            {
                return (rows, new RestoreResult(RestoreOutcome.RowsRefused, table == Chain ? $"o evento {-answer} não voltou como a cópia o tem" : $"o banco recusou a tabela {table}"));
            }
            rows += answer;
        }
        return (rows, null);
    }

    private async Task<RestoreResult> ConferAsync(RestoreCopy copy, RestoreTip tip, long rows, CancellationToken cancellationToken)
    {
        var verified = await new CustodyVerifier(dataSource, time).VerifyAsync(cancellationToken).ConfigureAwait(false);
        if (verified.FirstInconsistent is { } broken)
        {
            return new RestoreResult(RestoreOutcome.ChainBroken, $"o evento {broken.Sequence} da cadeia restaurada não confere ({broken.Kind})");
        }

        var conferred = await guarantees.VerifyAsync(cancellationToken).ConfigureAwait(false);
        var after = (await LastEventAsync(cancellationToken).ConfigureAwait(false))?.Sequence ?? 0;
        var held = conferred.Missing == GuaranteeKinds.None;
        var recorded = await new CustodyChain(dataSource)
            .AppendAsync(CustodyEvent.Restored(rows, held, $"copy={copy.Name};before={tip.Sequence};after={after}"), cancellationToken).ConfigureAwait(false);
        if (!held)
        {
            return new RestoreResult(RestoreOutcome.GuaranteesMissing, $"faltam as garantias {conferred.Missing}");
        }
        return recorded.Outcome == CustodyOutcome.Appended
            ? new RestoreResult(RestoreOutcome.Restored, string.Empty)
            : new RestoreResult(RestoreOutcome.ChainBroken, "a cadeia não aceitou o evento da restauração");
    }

    private async Task<long> LoadBatchAsync(string table, string rows, CancellationToken cancellationToken)
    {
        var rowsParameter = new NpgsqlParameter("rows", NpgsqlDbType.Jsonb) { Value = rows };
        var answer = table == Chain
            ? await ScalarAsync("SELECT public.restore_events(@rows)", cancellationToken, rowsParameter).ConfigureAwait(false)
            : await ScalarAsync("SELECT public.restore_load(@table, @rows)", cancellationToken, new NpgsqlParameter("table", table), rowsParameter).ConfigureAwait(false);
        return Convert.ToInt64(answer, CultureInfo.InvariantCulture);
    }

    private async Task<string?> RefusalOfOpeningAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await ScalarAsync("SELECT public.restore_open()", cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (PostgresException refused) when (refused.SqlState == "CE005")
        {
            return refused.MessageText;
        }
    }

    private Task<object?> CloseAsync(bool approved, CancellationToken cancellationToken) =>
        ScalarAsync("SELECT public.restore_close(@approved)", cancellationToken, new NpgsqlParameter("approved", approved));

    private async Task<RestoreTip?> LastEventAsync(CancellationToken cancellationToken)
    {
        var tip = (string?)await ScalarAsync(Tip, cancellationToken).ConfigureAwait(false);
        return tip?.Split(':') is [var sequence, var digest] ? new RestoreTip(long.Parse(sequence, CultureInfo.InvariantCulture), digest) : null;
    }

    private async Task<object?> ScalarAsync(string statement, CancellationToken cancellationToken, params NpgsqlParameter[] parameters)
    {
        var command = dataSource.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            command.Parameters.AddRange(parameters);
            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is DBNull ? null : result;
        }
    }
}
