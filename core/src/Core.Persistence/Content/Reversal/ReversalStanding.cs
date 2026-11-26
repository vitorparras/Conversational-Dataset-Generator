using System.Text.Json;
using Core.Ingestion.Contracts.Reversal;
using Core.Persistence.Content.Reading;
using Core.Persistence.Custody.Chain;
using Core.Persistence.People;
using Npgsql;
using NpgsqlTypes;
using static Core.Persistence.Content.Rerun.RerunRows;

namespace Core.Persistence.Content.Reversal;

/// <summary>Where a request stands, whether a person is of the curation, and why a request asks and what for as they go sealed.</summary>
internal static class ReversalStanding
{
    private const string FunctionOfPerson = "SELECT function_name FROM public.person WHERE id = @person";

    internal static async Task<ListedReversal?> ReadAsync(CustodyScope scope, long requestId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Bound[] request = [new("id", NpgsqlDbType.Bigint, requestId), new("now", NpgsqlDbType.TimestampTz, now)];
        var rows = await RowsAsync(scope, (ContentQueries.ReadReversalRequest, request), Listed, cancellationToken).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    internal static ListedReversal Listed(NpgsqlDataReader row)
    {
        var at = 0;
        return new ListedReversal
        {
            Id = row.GetInt64(at++),
            ItemId = row.GetInt64(at++),
            Marker = row.GetString(at++),
            TermDays = row.GetInt32(at++),
            RequestedBy = row.GetInt64(at++),
            RequestedAt = row.GetFieldValue<DateTimeOffset>(at++),
            AuthorizedBy = Held(row.IsDBNull, at++, row.GetInt64),
            ValidUntil = Held(row.IsDBNull, at++, row.GetFieldValue<DateTimeOffset>),
            State = Enum.Parse<ReversalState>(row.GetString(at++), ignoreCase: true),
            AuthorizationId = Held(row.IsDBNull, at++, row.GetInt64),
            RevealedAt = Held(row.IsDBNull, at, row.GetFieldValue<DateTimeOffset>),
        };
    }

    internal static async Task<bool> IsCurationAsync(CustodyScope scope, long personId, CancellationToken cancellationToken) =>
        await ScalarAsync(scope, (FunctionOfPerson, [new("person", NpgsqlDbType.Bigint, personId)]), cancellationToken).ConfigureAwait(false) is string function
        && string.Equals(function, PersonFunctions.Curation, StringComparison.Ordinal);

    internal static string Sealable(ReversalRequest request) => JsonSerializer.Serialize(new Justification(request.Reason, request.Use));

    internal static ReversalJustification Opened(ContentReadOutcome outcome, string text)
    {
        var opened = JsonSerializer.Deserialize<Justification>(text)!;
        return new ReversalJustification(outcome, opened.Reason, opened.Use);
    }

    private static T? Held<T>(Func<int, bool> absent, int at, Func<int, T> read)
        where T : struct => absent(at) ? null : read(at);

    /// <summary>Why the request asks and what for, as they go sealed in its row.</summary>
    private sealed record Justification(string Reason, string Use);
}
