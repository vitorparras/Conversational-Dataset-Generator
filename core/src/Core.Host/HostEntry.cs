using Core.Host.Migration;
using Core.Host.Restore;
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

        return OneShotModes.RunAsync(args) is { } oneShot ? await oneShot : await ConfiguredAsync(args, target);
    }

    /// <summary>The modes that read the configuration of the host: the migration, the comparison of the log, or the service itself.</summary>
    private static async Task<HostLaunch> ConfiguredAsync(string[] args, StartupReport target)
    {
        var offline = IsOffline(args);
        var configurationArguments = RestoreLoadMode.ConfigurationArguments(AuditCompareMode.ConfigurationArguments(MigrationMode.ConfigurationArguments(args)));
        var loaded = HostConfiguration.Load(configurationArguments, offline, AuditCompareMode.Requested(args));
        var database = HostConfiguration.DatabaseOf(loaded, offline);
        if (database is null || database.State == DatabaseSettingsState.Refused)
        {
            return HostLaunch.Refused(database?.Problems ?? loaded.Problems);
        }

        if (offline)
        {
            return await OfflineAsync(args, loaded, database);
        }

        var analyzeTimeout = LanguageServiceConfiguration.AnalyzeTimeout(loaded.Values);
        var refused = HostConfiguration.Refusals(loaded.Values, analyzeTimeout);
        return analyzeTimeout is { } timeout && refused.Count == 0 ? await CompositionRoot.LaunchAsync(loaded, database, timeout, target) : HostLaunch.Refused(refused);
    }

    /// <summary>Whether the arguments ask for a mode that serves nothing: the migration, the comparison of the log or the load of a restored copy.</summary>
    private static bool IsOffline(string[] args) =>
        MigrationMode.Requested(args) || AuditCompareMode.Requested(args) || RestoreLoadMode.Requested(args);

    /// <summary>The modes that serve nothing: the load of a restored copy, the comparison of the log with the chain, or the migration, which first judges the passwords.</summary>
    private static async Task<HostLaunch> OfflineAsync(string[] args, ConfigurationLoadResult loaded, DatabaseSettings database)
    {
        if (RestoreLoadMode.Requested(args))
        {
            return await RestoreLoadMode.LaunchAsync(args, database);
        }

        if (AuditCompareMode.Requested(args))
        {
            return await AuditCompareMode.LaunchAsync(loaded, database);
        }

        return PasswordConfiguration.Refusals(loaded.Values) is { Count: > 0 } invalid ? HostLaunch.Refused(invalid) : await CompositionRoot.MigrateAsync(database);
    }
}
