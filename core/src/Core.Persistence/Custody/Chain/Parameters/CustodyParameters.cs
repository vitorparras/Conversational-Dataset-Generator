using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Custody.Chain.Parameters;

/// <summary>The parameters of the statement that adds an event: what the event holds, bound by name and type, with the absent ones as database nulls.</summary>
internal static class CustodyParameters
{
    internal static void Bind(NpgsqlCommand command, CustodyEvent pending)
    {
        _ = command.Parameters.AddWithValue("actor", NpgsqlDbType.Bigint, Optional(pending.Author.PersonId));
        _ = command.Parameters.AddWithValue("system", NpgsqlDbType.Boolean, pending.Author.PersonId is null);
        _ = command.Parameters.AddWithValue("kind", NpgsqlDbType.Text, CustodyNames.Of(pending.Item.Kind));
        _ = command.Parameters.AddWithValue("item", NpgsqlDbType.Bigint, pending.Item.Id);
        _ = command.Parameters.AddWithValue("step", NpgsqlDbType.Text, CustodyNames.Of(pending.Step));
        _ = command.Parameters.AddWithValue("reason", NpgsqlDbType.Text, CustodyNames.Of(pending.Reason));
        _ = command.Parameters.AddWithValue("corrects", NpgsqlDbType.Bigint, Optional(pending.Corrects));
    }

    private static object Optional(long? value) => value is { } present ? present : DBNull.Value;
}
