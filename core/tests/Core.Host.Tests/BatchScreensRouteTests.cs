using System.Diagnostics.CodeAnalysis;
using System.Net;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The screen of the batches (issue 608) behind the real gates and over a database of its own with two domains and one batch in each: the curation sees the batches of the domains granted
/// alone, with the domain and the person by name (CA-1); with no domain granted it sees the list empty with the motive, and no other function gets more than the 404 of an address that does
/// not exist (CA-3); and a page past the ceiling of the register is the last page, never an error.
/// </summary>
[Trait("Category", "Database")]
public sealed class BatchScreensRouteTests
{
    private const string Granted = "zqx-dominio-a";
    private const string Other = "zqx-dominio-b";
    private const string CuratorName = "zqx-curadora";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task TheCurationSeesTheBatchesOfTheDomainsGrantedAloneWithTheDomainAndThePersonByName()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var page = await world.PageAsync("/lotes", world.Curator);

        Assert.Contains("Lote 1", page, StringComparison.Ordinal);
        Assert.Contains(Granted, page, StringComparison.Ordinal);
        Assert.Contains(CuratorName, page, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote 2", page, StringComparison.Ordinal);
        Assert.DoesNotContain(Other, page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheFilterOfTheAddressCutsTheListInTheServer()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var page = await world.PageAsync("/lotes?estado=rejeitados", world.Curator);

        Assert.DoesNotContain("Lote 1", page, StringComparison.Ordinal);
        Assert.Contains("Nenhum lote com esse filtro.", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task WithNoDomainGrantedTheCurationSeesTheListEmptyWithTheMotiveAndNoFilter()
    {
        await using var world = await World.StartAsync(grantsCurator: false);

        var page = await world.PageAsync("/lotes", world.Curator);

        Assert.Contains("Você não tem concessão em nenhum domínio.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Lote 1", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AnotherFunctionIsAnsweredAsForAnAddressThatDoesNotExist()
    {
        await using var world = await World.StartAsync(grantsCurator: true);
        var session = world.Host.Sessions.Open(world.Annotator);

        using var refused = await world.Host.GetAsync("/lotes", session, encrypted: true, Token);
        using var missing = await world.Host.GetAsync("/zqx-nao-existe-6080", session, encrypted: true, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (refused.StatusCode, missing.StatusCode));
    }

    [DatabaseFact]
    public async Task APagePastTheCeilingOfTheRegisterIsTheLastPageAndNeverAnError()
    {
        await using var world = await World.StartAsync(grantsCurator: true);

        var page = await world.PageAsync("/lotes?pagina=999999", world.Curator);

        Assert.Contains("Nenhum lote com esse filtro.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("pagina=501", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoDatabaseDeclaredTheHostSaysItCannot()
    {
        await using var host = await SessionedHost.StartAsync(services => services, BatchScreensEndpoint.Map, Token);

        using var listed = await host.GetAsync("/lotes", host.Sessions.Open(7), encrypted: true, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, listed.StatusCode);
    }

    private sealed class World : IAsyncDisposable
    {
        private const string PersonOf = "SELECT id FROM public.person WHERE name = @name";

        private readonly ScratchDatabase database;
        private readonly NpgsqlDataSource source;

        private World(ScratchDatabase database, NpgsqlDataSource source, SessionedHost host, long curator, long annotator)
        {
            this.database = database;
            this.source = source;
            Host = host;
            Curator = curator;
            Annotator = annotator;
        }

        internal SessionedHost Host { get; }

        internal long Curator { get; }

        internal long Annotator { get; }

        internal static async Task<World> StartAsync(bool grantsCurator)
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            await database.RunAsOwnerAsync(
                "INSERT INTO public.domain (name) VALUES ('zqx-dominio-a'), ('zqx-dominio-b')",
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-curadora', 'Curadoria do acervo')",
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-anotadora', 'Anotação')");
            await database.RunAsOwnerAsync(SeededBatches.Statements(1, 2));
            var (curator, annotator) = (await PersonIdAsync(database, CuratorName), await PersonIdAsync(database, "zqx-anotadora"));
            var source = NpgsqlDataSource.Create(database.AppConnectionString);
            var staff = new OperatorAdministration(
                new PersonRegistry(source),
                new PersonCredentials(source, new PasswordDerivation(1)),
                new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System),
                new PersonNotices(source));
            var grants = new FilledGrants(grantsCurator ? curator : 0, Granted);
            var host = await SessionedHost.StartAsync(
                services => services.AddSingleton(staff).AddSingleton(new BatchRegister(source)).AddSingleton(new DomainListing(source)).AddSingleton<DomainGrants>(grants),
                routes =>
                {
                    ScreenGate.Use((IApplicationBuilder)routes);
                    BatchScreensEndpoint.Map(routes);
                },
                Token);
            return new World(database, source, host, curator, annotator);
        }

        internal async Task<string> PageAsync(string route, long person)
        {
            using var response = await Host.GetAsync(route, Host.Sessions.Open(person), encrypted: true, Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync(Token);
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            await source.DisposeAsync();
            await database.DisposeAsync();
        }

        [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal, never input.")]
        private static async Task<long> PersonIdAsync(ScratchDatabase database, string name)
        {
            await using var connection = new NpgsqlConnection(database.OwnerConnectionString);
            await connection.OpenAsync(Token);
            await using var command = new NpgsqlCommand(PersonOf, connection);
            _ = command.Parameters.AddWithValue("name", name);
            return (long)(await command.ExecuteScalarAsync(Token))!;
        }
    }

    private sealed class FilledGrants : DomainGrants
    {
        private readonly long person;
        private readonly string[] domains;

        internal FilledGrants(long person, params string[] domains)
        {
            this.person = person;
            this.domains = domains;
        }

        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult(entered == person ? domains.AsEnumerable() : None);
    }
}
