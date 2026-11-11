using Core.Persistence.People;
using Core.Persistence.Settings;
using Npgsql;

namespace Core.Host.Settings;

/// <summary>What the two desks of the changes of configuration (decision 0053) take from the database; the root builds the desks over it. With no database there are no parts.</summary>
internal sealed class ChangeParts
{
    private ChangeParts(NpgsqlDataSource source)
    {
        Changes = new ConfigurationChanges(source);
        Registry = new PersonRegistry(source);
        Approved = new ApprovedChanges(source);
    }

    /// <summary>The ledger of the changes.</summary>
    internal ConfigurationChanges Changes { get; }
    /// <summary>The registry of who submits and decides.</summary>
    internal PersonRegistry Registry { get; }
    /// <summary>The approved changes, with the ones the core did not apply.</summary>
    internal ApprovedChanges Approved { get; }

    /// <summary>The parts over the startup container's data source; none when no database is declared.</summary>
    internal static ChangeParts? Of(IServiceProvider provider) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new ChangeParts(source) : null;

    /// <summary>Adds the desk of the submissions, the desk of the decisions and the approved changes the screen lists.</summary>
    internal static void Register(IServiceCollection services, ConfigurationLoadResult inForce) =>
        _ = services
            .AddSingleton(provider => Of(provider) is { } p ? new ConfigurationChangeDesk(inForce, p.Changes, p.Registry) : null!)
            .AddSingleton(provider => Of(provider) is { } p ? new ChangeDecisionDesk(inForce, p.Changes, p.Registry, provider.GetRequiredService<TimeProvider>()) : null!)
            .AddSingleton(provider => Of(provider) is { } p ? p.Approved : null!);
}
