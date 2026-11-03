using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Content;

/// <summary>The losses of a key declared in the chain, which only the chain says: the scopes, each as the declaration wrote it.</summary>
internal static class DeclaredKeyLoss
{
    internal static async Task<IReadOnlySet<string>> ScopesAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var command = dataSource.CreateCommand(CustodyStatements.ReadKeyLossScopes);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(CustodyStep.KeyLossDeclared));
            return await ReadAsync(command, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlySet<string>> ReadAsync(NpgsqlCommand command, CancellationToken cancellationToken)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using (reader.ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                _ = scopes.Add(reader.GetString(0));
            }
        }

        return scopes;
    }
}
