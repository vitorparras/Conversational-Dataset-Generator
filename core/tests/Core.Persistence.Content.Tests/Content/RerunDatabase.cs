using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// One database of its own, migrated once, for all the tests of a class about the rerun: the queue takes every item of the domain, so each test takes a domain no other test uses, with its own batch
/// accepted (numbered as the domain) and the labels of its key loaded. The database is made on first use, so a class whose tests are skipped for want of Docker never asks for one.
/// </summary>
[SuppressMessage("Design", "CA1515", Justification = "xunit builds the class fixture and the public test classes take it in their constructors.")]
public sealed class RerunDatabase : IAsyncDisposable
{
    private const int Domains = 20;

    private ScratchDatabase? database;
    private NpgsqlDataSource? application;
    private SyntheticCustody? custody;
    private int taken;

    /// <summary>A domain no other test of the class has taken, over the database, made on the first call.</summary>
    internal async Task<RerunCase> CaseAsync()
    {
        if (database is null)
        {
            database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var domains = Enumerable.Range(1, Domains).ToArray();
            await database.RunAsOwnerAsync(
                [
                    .. domains.Select(domain => $"INSERT INTO public.domain (name) VALUES ('zqx-domain-{domain}')"),
                    "INSERT INTO public.person (name, function_name) VALUES ('zqx-curator', 'Curadoria do acervo')",
                ]);
            await SeededBatches.SeedAsync(database, domains);
            custody = SyntheticCustody.Holding(domains.SelectMany(domain => new[] { $"texto-bruto/{domain}/v1", $"mapa-reversao/{domain}/v1" }));
            application = NpgsqlDataSource.Create(database.AppConnectionString);
        }

        var domainId = Interlocked.Increment(ref taken);
        Assert.True(domainId <= Domains, "The class has more tests than the fixture has domains.");
        return new RerunCase(database, application!, custody!, domainId);
    }

    public async ValueTask DisposeAsync()
    {
        if (application is not null)
        {
            await application.DisposeAsync();
        }

        custody?.Dispose();
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }
}
