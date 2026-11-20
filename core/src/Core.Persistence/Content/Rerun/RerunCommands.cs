using System.Data;
using System.Globalization;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Chain.Append;
using Core.Persistence.People;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content.Rerun;

/// <summary>What the queue and the rerun both write: the conclusion of a held item with its event, a try spent, and an item given back.</summary>
internal static class RerunCommands
{
    /// <summary>Declares the domain in a transaction the chain opened.</summary>
    internal static async Task DeclareAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(ContentTransaction.Declare);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("domain", domainId.ToString(CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The command over the entry of the hold, which still matches only while the hold is this one.</summary>
    internal static NpgsqlCommand Held(CustodyScope scope, string statement, RerunHold hold, DateTimeOffset now)
    {
        var command = scope.CreateCommand(statement);
        PersonQueries.Add(command, "entry", NpgsqlDbType.Bigint, hold.Entry);
        PersonQueries.Add(command, "held", NpgsqlDbType.TimestampTz, hold.Until);
        AddIfNamed(command, "now", NpgsqlDbType.TimestampTz, now);
        AddIfNamed(command, "tries", NpgsqlDbType.Integer, SanitizationReruns.Tries);
        return command;
    }

    /// <summary>Binds the version, the moment, and the end of a hold or the tries where the statement names them.</summary>
    internal static Action<NpgsqlCommand> Over(string version, DateTimeOffset now) => command =>
    {
        PersonQueries.Add(command, "version", NpgsqlDbType.Text, version);
        PersonQueries.Add(command, "now", NpgsqlDbType.TimestampTz, now);
        AddIfNamed(command, "until", NpgsqlDbType.TimestampTz, now + SanitizationReruns.Hold);
        AddIfNamed(command, "tries", NpgsqlDbType.Integer, SanitizationReruns.Tries);
    };

    /// <summary>Binds the parameter only when the statement of the command names it.</summary>
    internal static void AddIfNamed(NpgsqlCommand command, string name, NpgsqlDbType type, object value)
    {
        if (command.CommandText.Contains("@" + name, StringComparison.Ordinal))
        {
            PersonQueries.Add(command, name, type, value);
        }
    }

    /// <summary>Records the result of the item and concludes it in the queue, the event first; null when the hold is no longer this one, and then nothing is to be kept.</summary>
    internal static async Task<CustodyResult?> ConcludeAsync(
        CustodyScope scope, RerunHold hold, (CustodyReason Reason, RerunRefusal? Refusal, DateTimeOffset Now) result, CancellationToken cancellationToken)
    {
        var appended = await CustodyAppend.AppendAsync(scope, CustodyEvent.ItemResanitized(hold.ItemId, result.Reason, hold.Version), cancellationToken).ConfigureAwait(false);
        if (appended.Outcome != CustodyOutcome.Appended)
        {
            return appended;
        }

        var command = Held(scope, ContentQueries.CloseRerunItem, hold, result.Now);
        await using (command.ConfigureAwait(false))
        {
            PersonQueries.Add(command, "result", NpgsqlDbType.Text, ResultOf(result.Reason));
            PersonQueries.Add(command, "refusal", NpgsqlDbType.Text, result.Refusal is { } refusal ? NameOf(refusal) : null);
            return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null ? null : appended;
        }
    }

    /// <summary>Records each item a statement concluded, with the result and the version given; the last result, or none when it concluded none.</summary>
    internal static async Task<CustodyResult?> RecordEachAsync(CustodyScope scope, IEnumerable<(long Item, string Version)> concluded, CustodyReason reason, CancellationToken cancellationToken)
    {
        CustodyResult? last = null;
        foreach (var (item, version) in concluded)
        {
            last = await CustodyAppend.AppendAsync(scope, CustodyEvent.ItemResanitized(item, reason, version), cancellationToken).ConfigureAwait(false);
            if (last.Outcome != CustodyOutcome.Appended)
            {
                return last;
            }
        }

        return last;
    }

    /// <summary>The items a statement concluded, each with the version given or, without one, the version the statement answers beside it.</summary>
    internal static Task<List<(long Item, string Version)>> ConcludedAsync(
        CustodyScope scope, (string Statement, Action<NpgsqlCommand> Bind) query, string? version, CancellationToken cancellationToken) =>
        RerunReading.RowsAsync(scope, query, row => (row.GetInt64(0), version ?? row.GetString(1)), cancellationToken);

    /// <summary>Spends a try of the held item: it goes back to the queue, or is refused at the last try, with its event.</summary>
    internal static async Task<RerunOutcome> FailAsync(NpgsqlDataSource dataSource, RerunHold hold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, hold.DomainId, IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var scope = transaction.AsCustodyScope();
            string? state;
            var command = Held(scope, ContentQueries.CloseRerunFailed, hold, now);
            await using (command.ConfigureAwait(false))
            {
                state = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            }

            var recorded = state is "done" ? await RecordEachAsync(scope, [(hold.ItemId, hold.Version)], CustodyReason.SanitizationRefused, cancellationToken).ConfigureAwait(false) : null;
            if (state is null || recorded is { Outcome: not CustodyOutcome.Appended })
            {
                return state is null ? RerunOutcome.NotHeld : RerunOutcome.NotRecorded;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return recorded is null ? RerunOutcome.Failed : RerunOutcome.Refused;
        }
    }

    /// <summary>Gives the held item back to the queue without spending a try; false when the hold is no longer this one.</summary>
    internal static async Task<bool> ReturnAsync(NpgsqlDataSource dataSource, RerunHold hold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var transaction = await ContentTransaction.BeginAsync(dataSource, hold.DomainId, IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            var command = Held(transaction.AsCustodyScope(), ContentQueries.CloseRerunReturned, hold, now);
            int returned;
            await using (command.ConfigureAwait(false))
            {
                returned = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return returned == 1;
        }
    }

    internal static string NameOf(RerunRefusal refusal) => refusal switch
    {
        RerunRefusal.Emptied => "emptied",
        RerunRefusal.KeyLost => "key_lost",
        RerunRefusal.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "The refusal is not one the queue keeps."),
    };

    private static string ResultOf(CustodyReason reason)
    {
        if (reason == CustodyReason.SanitizationIdentical)
        {
            return "identical";
        }

        return reason == CustodyReason.SanitizationReplaced ? "replaced" : "refused";
    }
}
