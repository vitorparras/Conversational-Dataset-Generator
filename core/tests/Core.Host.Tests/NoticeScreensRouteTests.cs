using System.Diagnostics.CodeAnalysis;
using System.Net;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.Custody;
using Core.Persistence.Migrations;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The screen of the notices of the person of the session (issue 595, CA-2 and CA-4), behind the real session gate and over a database of its own: the list holds the unread notices of the
/// asker only, with the words of the code and the number of sessions; reading one is a post answered with a redirect to the list; a notice that is not the asker's, or that does not exist,
/// is one answer to the byte; and the notice read stays recorded.
/// </summary>
[Trait("Category", "Database")]
public sealed class NoticeScreensRouteTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task TheListHoldsTheUnreadNoticesOfTheAskerOnlyWithTheWordsOfTheCodeAndTheNumber()
    {
        await using var world = await World.StartAsync();
        var (mine, other) = (await world.NewPersonAsync(), await world.NewPersonAsync());
        await world.Notices.RecordPasswordResetAsync(mine, 2, Token);
        await world.Notices.RecordPasswordResetAsync(other, 5, Token);

        var page = await world.PageAsync("/avisos", world.Host.Sessions.Open(mine));

        Assert.Contains("2 sess", page, StringComparison.Ordinal);
        Assert.Contains("foram encerradas", page, StringComparison.Ordinal);
        Assert.DoesNotContain("5 sess", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ReadingANoticeRedirectsToTheListWhichNoLongerHoldsItAndTheRowStaysRecorded()
    {
        await using var world = await World.StartAsync();
        var person = await world.NewPersonAsync();
        await world.Notices.RecordPasswordResetAsync(person, 1, Token);
        var (session, id) = (world.Host.Sessions.Open(person), (await world.Notices.UnreadAsync(person, Token))[0].Id);

        using var read = await world.PostAsync($"/avisos/{id}/lido", session);

        Assert.Equal((HttpStatusCode.SeeOther, "/avisos?lido"), (read.StatusCode, read.Headers.Location?.OriginalString));
        Assert.Empty(await world.Notices.UnreadAsync(person, Token));
        Assert.Equal("1", await world.OwnerTextAsync($"SELECT count(*) FROM public.person_notice WHERE id = {id} AND read_at IS NOT NULL"));
    }

    [DatabaseFact]
    public async Task PostingAgainTheReadOfANoticeAlreadyReadGoesBackToTheList()
    {
        await using var world = await World.StartAsync();
        var person = await world.NewPersonAsync();
        await world.Notices.RecordPasswordResetAsync(person, 1, Token);
        var (session, id) = (world.Host.Sessions.Open(person), (await world.Notices.UnreadAsync(person, Token))[0].Id);
        using var first = await world.PostAsync($"/avisos/{id}/lido", session);

        using var again = await world.PostAsync($"/avisos/{id}/lido", session);

        Assert.Equal((HttpStatusCode.SeeOther, "/avisos?lido"), (again.StatusCode, again.Headers.Location?.OriginalString));
    }

    [DatabaseFact]
    public async Task ANoticeThatIsNotTheAskersOrDoesNotExistIsOneAnswerToTheByteAndNothingIsRead()
    {
        await using var world = await World.StartAsync();
        var (owner, asker) = (await world.NewPersonAsync(), await world.NewPersonAsync());
        await world.Notices.RecordPasswordResetAsync(owner, 1, Token);
        var (session, id) = (world.Host.Sessions.Open(asker), (await world.Notices.UnreadAsync(owner, Token))[0].Id);

        using var theirs = await world.PostAsync($"/avisos/{id}/lido", session);
        using var missing = await world.PostAsync("/avisos/999999999/lido", session);
        using var notANumber = await world.PostAsync("/avisos/abc/lido", session);

        var answers = new List<(HttpStatusCode Status, string Body)>();
        foreach (var response in new[] { theirs, missing, notANumber })
        {
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync(Token)));
        }

        Assert.Equal(HttpStatusCode.NotFound, Assert.Single(answers.Distinct()).Status);
        _ = Assert.Single(await world.Notices.UnreadAsync(owner, Token));
    }

    [DatabaseFact]
    public async Task WithNoSessionTheListAndTheReadAreUnauthorized()
    {
        await using var world = await World.StartAsync();

        using var listed = await world.Host.GetAsync("/avisos", identifier: null, encrypted: true, Token);
        using var read = await world.PostAsync("/avisos/1/lido", identifier: null);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (listed.StatusCode, read.StatusCode));
    }

    [Fact]
    public async Task WithNoDatabaseDeclaredTheHostSaysItCannot()
    {
        await using var host = await SessionedHost.StartAsync(services => services, NoticeScreensEndpoint.Map, Token);

        using var listed = await host.GetAsync("/avisos", host.Sessions.Open(7), encrypted: true, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, listed.StatusCode);
    }

    private sealed class World : IAsyncDisposable
    {
        private readonly ScratchDatabase database;
        private readonly NpgsqlDataSource source;

        private World(ScratchDatabase database, NpgsqlDataSource source, SessionedHost host)
        {
            this.database = database;
            this.source = source;
            Host = host;
            Notices = new PersonNotices(source);
        }

        internal SessionedHost Host { get; }

        internal PersonNotices Notices { get; }

        internal static async Task<World> StartAsync()
        {
            var database = await ScratchDatabase.CreateAsync();
            _ = await database.MigrateAsync(EmbeddedMigrations.Load());
            var source = NpgsqlDataSource.Create(database.AppConnectionString);
            var staff = new OperatorAdministration(
                new PersonRegistry(source),
                new PersonCredentials(source, new PasswordDerivation(1)),
                new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System),
                new PersonNotices(source));
            var host = await SessionedHost.StartAsync(services => services.AddSingleton(staff), NoticeScreensEndpoint.Map, Token);
            return new World(database, source, host);
        }

        internal async Task<long> NewPersonAsync()
        {
            var made = await new PersonRegistry(source).CreateAsync(
                new NewPerson($"zqx-pessoa-{Guid.NewGuid():N}", PersonFunctions.Accepted[0]), CustodyAuthor.System, Token);
            Assert.Equal(PersonOutcome.Done, made.Outcome);
            return made.PersonId!.Value;
        }

        internal async Task<string> PageAsync(string route, string identifier)
        {
            using var response = await Host.GetAsync(route, identifier, encrypted: true, Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return await response.Content.ReadAsStringAsync(Token);
        }

        internal Task<HttpResponseMessage> PostAsync(string route, string? identifier) =>
            Host.SendAsync(new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative)), identifier, encrypted: true, Token);

        [SuppressMessage("Security", "CA2100", Justification = "The statement is a literal of the calling test, never input.")]
        internal async Task<string> OwnerTextAsync(string statement)
        {
            await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
            await using var command = new NpgsqlCommand(statement, connection);
            return Convert.ToString(await command.ExecuteScalarAsync(Token), System.Globalization.CultureInfo.InvariantCulture)!;
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            await source.DisposeAsync();
            await database.DisposeAsync();
        }
    }
}
