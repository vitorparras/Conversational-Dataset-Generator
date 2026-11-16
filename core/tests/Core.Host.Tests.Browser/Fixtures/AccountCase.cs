using System.Collections.Concurrent;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the screens of the own account (issues 597 and 598) with the real routes, the real change of password and the real store of sessions. A change of password ends what the
/// person could do with the password the run made up, so each test takes a person of its own, made by <see cref="AddPersonAsync"/>; everything the host writes in its log is kept in
/// <see cref="Log"/>, so that a test can look for a password in it.
/// </summary>
internal sealed class AccountCase : IAsyncDisposable
{
    private readonly LogKeeper keeper;

    private AccountCase(ScreenHost host, LogKeeper keeper)
    {
        Host = host;
        this.keeper = keeper;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>Every line the host logged, with its scopes and its message, as text.</summary>
    public string Log => string.Join('\n', keeper.Lines);

    public static async Task<AccountCase> StartAsync(CancellationToken cancellationToken)
    {
        var keeper = new LogKeeper();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, _) => services.AddLogging(logging => logging.AddProvider(keeper))
                    .AddSingleton(provider => new SignInDelay(TimeProvider.System))
                    .AddSingleton(provider =>
                    {
                        var people = provider.GetRequiredService<OperatorAdministration>();
                        return new PasswordChange(people.Credentials, provider.GetRequiredService<SessionStore>(), provider.GetRequiredService<SignInDelay>());
                    }),
                Screens = AccountScreensEndpoint.Map,
            },
            cancellationToken);
        return new AccountCase(host, keeper);
    }

    /// <summary>A person of an annotation with the password of the run, made up for one test.</summary>
    public async Task<string> AddPersonAsync()
    {
        var name = $"zqx-conta-{Guid.NewGuid():N}";
        await Host.AddPersonAsync(name, PersonFunctions.Accepted[0], CancellationToken.None);
        return name;
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

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
