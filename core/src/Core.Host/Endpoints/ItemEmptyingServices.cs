using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// What the root registers for the screens of the emptying (#617): the source of the domains granted and the desk. They are factories and not registrations, because only the root registers;
/// the root names them, and the desk is built when a request first asks for it, from the data source of the startup container, as the other services that need the database are.
/// </summary>
internal static class ItemEmptyingServices
{
    /// <summary>The source of grants of production before #112: none is recorded, so it gives none. A test host registers its own after the root and it takes the place of this one.</summary>
    /// <param name="provider">The container of the host.</param>
    internal static DomainGrants GrantsOf(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return new NoDomainGrants();
    }

    /// <summary>The desk of the emptying; none when no database was declared, and the screen then says it is the host that cannot, as the other screens do.</summary>
    /// <param name="provider">The container of the host.</param>
    internal static ItemEmptyingDesk DeskOf(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source ? ItemEmptyingDesk.On(source) : null!;
    }
}
