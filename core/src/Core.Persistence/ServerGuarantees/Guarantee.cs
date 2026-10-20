using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>One guarantee as the probe runs it: its name, what the server must offer for the reading to run, and the reading.</summary>
internal sealed record Guarantee(string Name, string Requirement, Func<NpgsqlConnection, CancellationToken, Task<Finding>> ReadAsync);
