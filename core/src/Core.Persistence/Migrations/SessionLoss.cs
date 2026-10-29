using System.Data;

namespace Core.Persistence.Migrations;

/// <summary>
/// A session that goes away under a command shows as <see cref="InvalidOperationException"/> ("Connection is not open") on the next command
/// over it, not as <see cref="Npgsql.NpgsqlException"/>. It counts only while the connection is no longer open, so any other misuse still surfaces.
/// </summary>
internal static class SessionLoss
{
    internal static bool Is(IDbConnection connection) => connection.State != ConnectionState.Open;
}
