using Core.Persistence.People;
using Core.Persistence.Settings;
using Core.Platform.Configuration;
using Npgsql;

namespace Core.Host.Settings;

/// <summary>
/// The two desks of the changes of configuration (decision 0053), over the configuration the host started with and the database of the startup container. With no database declared
/// there is no desk, and the screen says it is the host that cannot, as the other screens do.
/// </summary>
internal static class ChangeParts
{
    /// <summary>Adds the desk of the submissions and the desk of the decisions.</summary>
    internal static void Register(IServiceCollection services, ConfigurationLoadResult inForce) =>
        _ = services
            .AddSingleton(provider => SourceOf(provider) is { } source
                ? new ConfigurationChangeDesk(inForce, new ConfigurationChanges(source), new PersonRegistry(source)) : null!)
            .AddSingleton(provider => SourceOf(provider) is { } source
                ? new ChangeDecisionDesk(inForce, new ConfigurationChanges(source), new PersonRegistry(source), provider.GetRequiredService<TimeProvider>()) : null!)
            .AddSingleton(provider => SourceOf(provider) is { } source ? new ApprovedChanges(source) : null!);

    private static NpgsqlDataSource? SourceOf(IServiceProvider provider) => provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>();
}
