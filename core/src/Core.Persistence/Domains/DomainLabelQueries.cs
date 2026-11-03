using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Domains;

/// <summary>What the rotation reads and writes inside its transaction: the newest generation a domain has labels for, and the registration of a label.</summary>
internal static class DomainLabelQueries
{
    private const string NewestVersion = """
        SELECT coalesce(max((substring(label FROM '/v([0-9]+)$'))::int), 0) FROM public.derivation_label WHERE label LIKE @pattern
        """;

    private const string InsertLabel = "INSERT INTO public.derivation_label (label) VALUES (@label)";

    /// <summary>The newest generation of the labels of the domain; 0 when it has none, as a domain that does not exist, since the creation writes the first generation with the domain.</summary>
    internal static async Task<int> NewestVersionAsync(CustodyScope scope, int domainId, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(NewestVersion);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("pattern", NpgsqlDbType.Text) { Value = $"%/{domainId}/v%" });
            return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
        }
    }

    /// <summary>Registers a label of a generation the domain passes to.</summary>
    internal static async Task AddLabelAsync(CustodyScope scope, string label, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(InsertLabel);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("label", NpgsqlDbType.Text) { Value = label });
            _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
