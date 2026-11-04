using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Domains;

/// <summary>What the rotation and the revocation read and write inside their transaction: the newest generation a domain has labels for, what is known of a label, and the two registrations.</summary>
internal static class DomainLabelQueries
{
    private const string NewestVersion = "SELECT coalesce(max((substring(label FROM '/v([0-9]+)$'))::int), 0) FROM public.derivation_label WHERE label LIKE @pattern";

    private const string InsertLabel = "INSERT INTO public.derivation_label (label) VALUES (@label)";

    private const string InsertRevocation = "INSERT INTO public.derivation_label_revocation (label) VALUES (@label)";

    private const string StateOfLabel = """
        SELECT
            EXISTS (SELECT 1 FROM public.derivation_label WHERE label = @label) AS known,
            EXISTS (SELECT 1 FROM public.derivation_label_revocation WHERE label = @label) AS revoked,
            EXISTS (
                SELECT 1 FROM public.derivation_label l
                WHERE l.label LIKE @prefix AND (substring(l.label FROM '/v([0-9]+)$'))::int > @version
                AND NOT EXISTS (SELECT 1 FROM public.derivation_label_revocation r WHERE r.label = l.label)) AS newer_kept
        """;

    /// <summary>The newest generation of the domain labels; 0 when it has none, as a domain that does not exist.</summary>
    internal static async Task<int> NewestVersionAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(NewestVersion);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("pattern", NpgsqlDbType.Text) { Value = $"%/{domainId}/v%" });
            return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    internal static Task AddLabelAsync(CustodyScope scope, string label, CancellationToken cancellationToken) => WriteAsync(scope, InsertLabel, label, cancellationToken);

    internal static Task AddRevocationAsync(CustodyScope scope, string label, CancellationToken cancellationToken) => WriteAsync(scope, InsertRevocation, label, cancellationToken);

    /// <summary>Whether the label is registered, whether it is revoked, and whether a newer generation of its purpose in its domain is left that is not.</summary>
    internal static async Task<LabelState> StateAsync(CustodyScope scope, DerivationLabel label, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(StateOfLabel);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("label", NpgsqlDbType.Text) { Value = label.Text });
            _ = command.Parameters.Add(new NpgsqlParameter("prefix", NpgsqlDbType.Text) { Value = label.Text.Remove(label.Text.LastIndexOf('/')) + "/v%" });
            _ = command.Parameters.Add(new NpgsqlParameter("version", NpgsqlDbType.Integer) { Value = label.Version });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                _ = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                return new LabelState((bool)reader["known"], (bool)reader["revoked"], (bool)reader["newer_kept"]);
            }
        }
    }

    private static async Task WriteAsync(CustodyScope scope, string statement, string label, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("label", NpgsqlDbType.Text) { Value = label });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
