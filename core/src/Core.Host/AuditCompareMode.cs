using Core.Persistence.Connection;
using Core.Persistence.ServerGuarantees;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Host;

/// <summary>
/// The mode of the executable that compares the server's audit log with the chain for an interval, once, and says what differs. It serves
/// nothing: it reads the log from the folder the server writes it to and the events through the application's own credential.
/// </summary>
internal static class AuditCompareMode
{
    private const string Argument = "audit-compare";

    internal static bool Requested(string[] args) => args is [Argument, ..];

    internal static string[] ConfigurationArguments(string[] args) => Requested(args) ? args[1..] : args;

    /// <summary>Compares the interval the configuration gave; a refusal of the interval or the database is the host's usual refusal.</summary>
    internal static async Task<HostLaunch> LaunchAsync(ConfigurationLoadResult loaded, DatabaseSettings database)
    {
        if (AuditConfiguration.Refusals(loaded.Values) is { Count: > 0 } refused)
        {
            return HostLaunch.Refused(refused);
        }

        var (from, to) = AuditConfiguration.Interval(loaded.Values);
        try
        {
            await using var source = NpgsqlDataSource.Create(database.ConnectionString!);
            var comparer = new AuditComparer(source, AuditConfiguration.ServerLogDirectory(loaded.Values));
            return HostLaunch.Compared(new AuditCompareOutcome(await comparer.CompareAsync(from, to, CancellationToken.None).ConfigureAwait(false), Failure: null));
        }
        catch (Exception failure) when (failure is NpgsqlException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return HostLaunch.Compared(new AuditCompareOutcome([], failure.GetType().FullName));
        }
    }
}
