using Core.Persistence.Notices;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Npgsql;

namespace Core.Host.Operators;

/// <summary>
/// What the root registers for the people who operate (#595): the administration over the registry, the passwords, the sessions and the notices. A factory and not a registration,
/// because only the root registers; with no database declared there is none.
/// </summary>
internal static class OperatorServices
{
    /// <summary>The administration of the people, built when a request first asks for it, from the data source of the startup container.</summary>
    /// <param name="provider">The container of the host.</param>
    internal static OperatorAdministration AdministrationOf(IServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source
            ? new OperatorAdministration(
                new PersonRegistry(source),
                new PersonCredentials(source, provider.GetRequiredService<PasswordDerivation>()),
                provider.GetRequiredService<SessionStore>(),
                new PersonNotices(source))
            : null!;
    }
}
