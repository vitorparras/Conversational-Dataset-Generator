using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Configuration;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The pages of the entry and of the exit on a real Kestrel, behind the real session gate and over a migrated database of its own: the real store of credentials, the real
/// delay of a refusal and the real session store. The registry holds nobody, or the one person of <see cref="Name"/> with the password of <see cref="Password"/>, both made up
/// for the run. The channel is told to be encrypted, which the loopback of the test cannot be; the entry itself is untouched. <see cref="HoldNextPost"/> keeps the post of the form
/// on the server, so that a test can look at what the browser does while the answer has not come.
/// </summary>
internal sealed class EntryScreenHost : IAsyncDisposable
{
    private readonly ScratchDatabase database;
    private readonly Lock gate = new();
    private WebApplication? app;
    private TaskCompletionSource? held;
    private int posts;

    private EntryScreenHost(ScratchDatabase database)
    {
        this.database = database;
    }

    public string Name { get; } = $"zqx-pessoa-{Guid.NewGuid():N}";

    public string Password { get; } = $"zqx-senha-{Guid.NewGuid():N}";

    public Uri Address { get; private set; } = new("http://127.0.0.1");

    /// <summary>How many posts of the entry reached the server.</summary>
    public int Posts
    {
        get
        {
            lock (gate)
            {
                return posts;
            }
        }
    }

    public static async Task<EntryScreenHost> StartAsync(bool withPerson, CancellationToken cancellationToken)
    {
        var database = await ScratchDatabase.CreateAsync();
        try
        {
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var builder = CompositionRoot.ClosedBuilder("http://127.0.0.1:0");
            CompositionRoot.Compose(builder, ClosedConfiguration.Load(new ConfigurationSchema([HostConfiguration.Section])).View, fileLog: null);
            var sessions = new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System);
            _ = builder.Services.AddSingleton(sessions).AddSingleton(_ => NpgsqlDataSource.Create(database.AppConnectionString)).AddSingleton(SignInOf);
            var app = builder.Build();
            var host = new EntryScreenHost(database);
            _ = app.Use(host.ServeAsync);
            SessionGate.Use(app);
            EntryPageEndpoint.Map(app);
            ScreenAssets.Map(app);
            await app.StartAsync(cancellationToken);
            host.app = app;
            host.Address = new Uri(app.Urls.Single());
            await host.AddPersonWhenAskedAsync(withPerson, cancellationToken);
            return host;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    /// <summary>From now on the next post of the entry waits on the server until the test calls <see cref="Release"/>.</summary>
    public void HoldNextPost()
    {
        lock (gate)
        {
            held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public void Release()
    {
        lock (gate)
        {
            _ = held?.TrySetResult();
            held = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        Release();
        if (app is not null)
        {
            await app.DisposeAsync();
        }

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

    /// <summary>Tells the channel to be encrypted and, for a post of the entry, counts it and keeps it while the test asks.</summary>
    private async Task ServeAsync(HttpContext context, RequestDelegate next)
    {
        context.Request.Scheme = "https";
        if (context.Request.Path.Equals(ScreenPaths.SignIn) && HttpMethods.IsPost(context.Request.Method))
        {
            Task? waiting;
            lock (gate)
            {
                posts++;
                waiting = held?.Task;
            }

            if (waiting is not null)
            {
                await waiting.WaitAsync(context.RequestAborted);
            }
        }

        await next(context);
    }

    private async Task AddPersonWhenAskedAsync(bool withPerson, CancellationToken cancellationToken)
    {
        if (!withPerson)
        {
            return;
        }

        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(Name, PersonFunctions.Accepted[0]), CustodyAuthor.System, cancellationToken);
        _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(created.PersonId!.Value, Password, CustodyAuthor.System, cancellationToken);
    }
}
