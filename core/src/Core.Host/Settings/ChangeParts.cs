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
        People = new PersonRegistry(source);
    }

    /// <summary>The ledger of the changes.</summary>
    internal ConfigurationChanges Changes { get; }

    /// <summary>The people who submit and decide.</summary>
    internal PersonRegistry People { get; }

    /// <summary>The parts over the startup container's data source; none when no database is declared.</summary>
    internal static ChangeParts? Of(IServiceProvider provider) =>
        provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? new ChangeParts(source) : null;
}
