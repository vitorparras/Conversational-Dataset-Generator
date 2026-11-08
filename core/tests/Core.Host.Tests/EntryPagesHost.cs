using Core.Host.Endpoints;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The routes of the entry and of the exit in process, behind the real gate, over a database of their own that is migrated and holds nobody or one person with a
/// password that the test made up when it ran. The entry is the real one: the store of credentials, the delay and the record in the chain. What the host logs is kept.
/// </summary>
internal sealed class EntryPagesHost : IAsyncDisposable
{
    private readonly ScratchDatabase database;

    private EntryPagesHost(ScratchDatabase database, SessionedHost host, RecordedLog log)
    {
        this.database = database;
        Host = host;
        Log = log;
    }

    public string Name { get; } = $"zqx-pessoa-{Guid.NewGuid():N}";

    public string Password { get; } = $"zqx-{Guid.NewGuid():N}";

    public SessionedHost Host { get; }

    public RecordedLog Log { get; }

    /// <summary>The host with the registry empty, or holding the person of <see cref="Name"/> when <paramref name="withPerson"/> says so.</summary>
    public static async Task<EntryPagesHost> StartAsync(bool withPerson, CancellationToken cancellationToken)
    {
        if (!DockerAvailability.ShouldRun)
        {
            Assert.Skip(DockerAvailability.SkipReason);
        }

        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        var log = new RecordedLog();
        var host = await SessionedHost.StartAsync(
            services => services.AddSingleton<ILoggerProvider>(log).AddSingleton(_ => NpgsqlDataSource.Create(database.AppConnectionString)).AddSingleton(SignInOf),
            routes =>
            {
                EntryPageEndpoint.Map(routes);
                SessionEndpoint.Map(routes);
            },
            cancellationToken);
        var entry = new EntryPagesHost(database, host, log);
        if (withPerson)
        {
            await entry.AddPersonAsync(cancellationToken);
        }

        return entry;
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await database.DisposeAsync();
    }

    private static SignIn SignInOf(IServiceProvider provider)
    {
        var source = provider.GetRequiredService<NpgsqlDataSource>();
        return new SignIn(
            new PersonRegistry(source),
            new PersonCredentials(source, new PasswordDerivation(1)),
            provider.GetRequiredService<SessionStore>(),
            new SignInDelay(TimeProvider.System),
            new SignInRecord(new SignInAttempts(source), provider.GetRequiredService<ILoggerFactory>()));
    }

    private async Task AddPersonAsync(CancellationToken cancellationToken)
    {
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(Name, PersonFunctions.Accepted[0]), CustodyAuthor.System, cancellationToken);
        _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(created.PersonId!.Value, Password, CustodyAuthor.System, cancellationToken);
    }
}
