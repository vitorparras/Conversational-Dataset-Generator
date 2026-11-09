using System.Collections.Concurrent;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screens of the own account (issues 597 and 598) with the real routes, the real change of password and the real store of sessions. A change of password ends what the
/// person could do with the password the run made up, so each test takes a person of its own, made by <see cref="AddPersonAsync"/>; everything the host writes in its log is kept in
/// <see cref="Log"/>, so that a test can look for a password in it.
/// </summary>
internal sealed class AccountScreenHost : ScreenHostBase
{
    private readonly LogKeeper keeper = new();

    private AccountScreenHost(ScratchDatabase database)
        : base(database)
    {
    }

    /// <summary>Every line the host logged, with its scopes and its message, as text.</summary>
    public string Log => string.Join('\n', keeper.Lines);

    public static Task<AccountScreenHost> StartAsync(CancellationToken cancellationToken) =>
        StartAsync(database => new AccountScreenHost(database), _ => Task.CompletedTask, cancellationToken);

    /// <summary>A person of an annotation with the password of the run, made up for one test.</summary>
    public async Task<string> AddPersonAsync()
    {
        var name = $"zqx-conta-{Guid.NewGuid():N}";
        await using var source = NpgsqlDataSource.Create(Database.AppConnectionString);
        var created = await new PersonRegistry(source).CreateAsync(new NewPerson(name, PersonFunctions.Accepted[0]), CustodyAuthor.System, CancellationToken.None);
        _ = await new PersonCredentials(source, new PasswordDerivation(1)).SetPasswordAsync(created.PersonId!.Value, Password, CustodyAuthor.System, CancellationToken.None);
        return name;
    }

    protected override void AddServices(IServiceCollection services)
    {
        _ = services.AddLogging(logging => logging.AddProvider(keeper));
        _ = services.AddSingleton(provider => new SignInDelay(TimeProvider.System))
            .AddSingleton(provider =>
            {
                var people = provider.GetRequiredService<OperatorAdministration>();
                return new PasswordChange(people.Credentials, provider.GetRequiredService<SessionStore>(), provider.GetRequiredService<SignInDelay>());
            });
    }

    protected override void MapScreens(IEndpointRouteBuilder routes) => AccountScreensEndpoint.Map(routes);

    private sealed class LogKeeper : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Keeping(categoryName, Lines);

        public void Dispose()
        {
        }

        private sealed class Keeping(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                ArgumentNullException.ThrowIfNull(formatter);
                lines.Enqueue($"{category}: {formatter(state, exception)} {exception}");
            }
        }
    }
}
