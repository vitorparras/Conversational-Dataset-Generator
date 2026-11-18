using System.Collections.Concurrent;
using Core.Host.Endpoints;
using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The host of the screens of the people (issue 599) with the real routes, the real registration and the real registry: besides the four people of the run, one of each function that
/// reaches a different part of the menu, it holds the people of <see cref="Listed"/>, enough of them for three pages of the list, with their functions turning among the six of the
/// policy and every tenth one deactivated. Everything the host writes in its log is kept in <see cref="Log"/>, so that a test can look for a password in it.
/// </summary>
internal sealed class PeopleCase : IAsyncDisposable
{
    /// <summary>The common start of the names of the people of the list, which a filter by name reaches and no person made by a test has.</summary>
    public const string Prefix = "zqx-lista-";

    private const int ListedCount = 46;
    private const int DeactivatedEvery = 10;

    private readonly LogKeeper keeper;

    private PeopleCase(ScreenHost host, LogKeeper keeper)
    {
        Host = host;
        this.keeper = keeper;
    }

    /// <summary>The host of the case.</summary>
    public ScreenHost Host { get; }

    /// <summary>The people of the list, in the order of their names, which is the order the screen shows them in.</summary>
    public static IReadOnlyList<Seed> Listed { get; } =
        [.. Enumerable.Range(1, ListedCount).Select(number => new Seed($"{Prefix}{number:00}", PersonFunctions.Accepted[number % PersonFunctions.Accepted.Count], number % DeactivatedEvery != 0))];

    /// <summary>Every line the host logged, with its category and its message, as text.</summary>
    public string Log => string.Join('\n', keeper.Lines);

    public static async Task<PeopleCase> StartAsync(CancellationToken cancellationToken)
    {
        var keeper = new LogKeeper();
        var host = await ScreenHost.StartAsync(
            new ScreenParts
            {
                Services = (services, _) => services.AddLogging(logging => logging.AddProvider(keeper)).AddSingleton(provider => new PersonEmails(provider.GetRequiredService<NpgsqlDataSource>())),
                Screens = routes =>
                {
                    OperatorScreensEndpoint.Map(routes);
                    AccountScreensEndpoint.Map(routes);
                },
                Prepare = SeedAsync,
            },
            cancellationToken);
        return new PeopleCase(host, keeper);
    }

    /// <summary>The whole database as text, as a copy of it would be taken, for a test to look for what must not be in it.</summary>
    public Task<string> DumpAsync() => Host.Database.DumpAsync();

    /// <summary>Deactivates the person of the id, as the administration would elsewhere, so that a test reaches the page of a person who was active a moment ago.</summary>
    public async Task DeactivateAsync(long id)
    {
        await using var source = NpgsqlDataSource.Create(Host.Database.AppConnectionString);
        _ = await new PersonRegistry(source).DeactivateAsync(id, CustodyAuthor.System, CancellationToken.None);
    }

    public ValueTask DisposeAsync() => Host.DisposeAsync();

    private static async Task SeedAsync(ScratchDatabase database)
    {
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var registry = new PersonRegistry(source);
        foreach (var seed in Listed)
        {
            var created = await registry.CreateAsync(new NewPerson(seed.Name, seed.Function), CustodyAuthor.System, CancellationToken.None);
            if (!seed.Active)
            {
                _ = await registry.DeactivateAsync(created.PersonId!.Value, CustodyAuthor.System, CancellationToken.None);
            }
        }
    }

    /// <summary>One person of the list: the name, the function and whether the person is active.</summary>
    internal sealed record Seed(string Name, string Function, bool Active);

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
