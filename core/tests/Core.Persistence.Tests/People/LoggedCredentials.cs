using Core.Persistence.People;
using Npgsql;

namespace Core.Persistence.Tests.People;

/// <summary>The store of passwords over a data source whose driver writes every line it logs, at any level, to <see cref="Log" />.</summary>
internal sealed class LoggedCredentials : IAsyncDisposable
{
    private readonly NpgsqlDataSource source;

    internal LoggedCredentials(string connectionString, int workFactor)
    {
        Log = new CapturedNpgsqlLog();
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _ = builder.UseLoggerFactory(Log);
        source = builder.Build();
        Store = new PersonCredentials(source, new PasswordDerivation(workFactor));
    }

    internal PersonCredentials Store { get; }

    internal CapturedNpgsqlLog Log { get; }

    public async ValueTask DisposeAsync()
    {
        await source.DisposeAsync();
        Log.Dispose();
    }
}
