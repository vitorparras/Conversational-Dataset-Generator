using Core.Host.Migration;
using Core.Host.Supervision;
using Core.Persistence.Connection;
using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>The way into the host: the target check, then the mode the arguments ask for, each judged by its own configuration before anything is assembled.</summary>
internal static class HostEntry
{
    internal static async Task<HostLaunch> BuildAsync(string[] args)
    {
        var target = await new StartupRunner([HostConfiguration.TargetCheck()], TimeProvider.System).RunAsync(CancellationToken.None);
        if (!target.Ready)
        {
            return HostLaunch.CheckFailed(target);
        }

        var migrating = MigrationMode.Requested(args);
        var comparing = AuditCompareMode.Requested(args);
        var offline = migrating || comparing;
        var loaded = HostConfiguration.Load(AuditCompareMode.ConfigurationArguments(MigrationMode.ConfigurationArguments(args)), offline, comparing);
        var database = HostConfiguration.DatabaseOf(loaded, offline);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        if (offline)
        {
            return await OfflineAsync(loaded, database, comparing);
        }

        var analyzeTimeout = LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values);
        var refused = HostConfiguration.Refusals(loaded.Values, analyzeTimeout);
        return analyzeTimeout is { } timeout && refused.Count == 0 ? await CompositionRoot.LaunchAsync(loaded, database, timeout, target) : HostLaunch.Refused(refused);
    }

    /// <summary>The modes that serve nothing: the comparison of the server's log with the chain, or the migration, which first judges the factor of the passwords.</summary>
    private static async Task<HostLaunch> OfflineAsync(ConfigurationLoadResult loaded, DatabaseSettings database, bool comparing)
    {
        if (comparing)
        {
            return await AuditCompareMode.LaunchAsync(loaded, database);
        }

        return PasswordConfiguration.Refusals(loaded.Values) is { Count: > 0 } invalid ? HostLaunch.Refused(invalid) : await CompositionRoot.MigrateAsync(database);
    }
}
