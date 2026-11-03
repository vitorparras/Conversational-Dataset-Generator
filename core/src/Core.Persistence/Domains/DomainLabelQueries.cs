using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Domains;

/// <summary>What the rotation and the revocation read inside their transaction: whether the domain exists, which labels it has and which of them are revoked, and the two writes.</summary>
internal static class DomainLabelQueries
{
    private const string DomainExists = "SELECT EXISTS (SELECT 1 FROM public.domain WHERE id = @id)";

    private const string LabelsOfDomain = """
        SELECT l.label, r.label IS NOT NULL
        FROM public.derivation_label l LEFT JOIN public.derivation_label_revocation r ON r.label = l.label
        WHERE l.label LIKE @pattern
        """;

    private const string InsertLabel = "INSERT INTO public.derivation_label (label) VALUES (@label)";
    private const string InsertRevocation = "INSERT INTO public.derivation_label_revocation (label) VALUES (@label)";

    internal static async Task<bool> ExistsAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(DomainExists);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Integer) { Value = domainId });
            return (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    /// <summary>The labels of the domain, each with whether it is revoked.</summary>
    internal static async Task<List<StoredLabel>> LabelsAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var stored = new List<StoredLabel>();
        var command = scope.CreateCommand(LabelsOfDomain);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("pattern", NpgsqlDbType.Text) { Value = $"%/{domainId}/v%" });
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (DerivationLabel.TryParse(reader.GetString(0), out var label) && label.DomainId == domainId)
                    {
                        stored.Add(new StoredLabel(label, reader.GetBoolean(1)));
                    }
                }
            }
        }

        return stored;
    }

    /// <summary>Registers a label of a generation the domain passes to.</summary>
    internal static Task AddLabelAsync(CustodyScope scope, string label, CancellationToken cancellationToken) => WriteAsync(scope, InsertLabel, label, cancellationToken);

    /// <summary>Registers the revocation of a label.</summary>
    internal static Task AddRevocationAsync(CustodyScope scope, string label, CancellationToken cancellationToken) => WriteAsync(scope, InsertRevocation, label, cancellationToken);

    private static async Task WriteAsync(CustodyScope scope, string statement, string label, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(statement);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("label", NpgsqlDbType.Text) { Value = label });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>A label of the domain and whether it is revoked.</summary>
    internal sealed record StoredLabel(DerivationLabel Label, bool Revoked);
}
