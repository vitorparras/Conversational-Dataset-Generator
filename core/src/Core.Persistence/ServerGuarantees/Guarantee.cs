using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// One guarantee as the probe runs it: its name, what the server must offer for the reading to run, and the reading.
/// A guarantee that reads the system catalog is refused on the catalog when the server denies it; one that reads its own table says so with its requirement.
/// </summary>
internal sealed record Guarantee(
    string Name, string Requirement, Func<NpgsqlConnection, CancellationToken, Task<Finding>> ReadAsync, bool ReadsCatalog = true);
