using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The routes of a session that make a person, deactivate one and set a password again (issue 65, part 2), on the built host over a database of its own: the author of each event is the
/// person of the session, only the administration function acts, the last administrator stays, the person deactivated loses the session and keeps what they signed, a password set again
/// is the one that opens, and a field not accepted is named. Only the status, the body and the rows are looked at.
/// </summary>
[Trait("Category", "Database")]
public sealed class PeopleRoutesProcessTests
{
    private const string Secret = "ZQX-route-secret-8841";
    private const string Another = "ZQX-another-secret-3307";
    private const string Auditing = "Auditoria";

    [DatabaseFact]
    public async Task Ca1TheThreeRoutesWriteTheirEventsWithTheAuthorTheSessionPerson()
    {
        await using var host = await AdministratorAsync();
        var admin = await IdOfAsync(host, host.Name);

        using var made = await host.PostAsync("/people", Person("zqx-made", Auditing, Secret), Token);
        var id = await IdFromAsync(made);
        using var reset = await host.SendAsAsync(HttpMethod.Put, $"/people/{id}/password", PasswordBody(Another), host.Identifier, Token);
        using var deactivated = await host.PostAsync($"/people/{id}/deactivate", "{}", Token);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.OK), (made.StatusCode, reset.StatusCode, deactivated.StatusCode));
        Assert.Equal(
            (1L, 2L, 1L),
            (await EventsAsync(host, "person_created", id, admin), await EventsAsync(host, "person_password_set", id, admin), await EventsAsync(host, "person_deactivated", id, admin)));
        Assert.Equal(0L, await CountAsync(host, $"public.custody_event WHERE item_id = {id} AND item_kind = 'person' AND actor_system"));
    }

    [DatabaseTheory]
    [InlineData("Anotação")]
    [InlineData("Auditoria")]
    public async Task Ca2APersonWithoutTheFunctionIsRefusedWithForbiddenAndNothingIsWritten(string function)
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup { Function = function }, Token);
        var other = await CreatedBySystemAsync(host, "zqx-other");
        var before = (await CountAsync(host, "public.person"), await CountAsync(host, "public.custody_event"));

        var answers = new[]
        {
            await StatusAsync(host.PostAsync("/people", Person("zqx-new", Auditing, Secret), Token)),
            await StatusAsync(host.PostAsync($"/people/{other}/deactivate", "{}", Token)),
            await StatusAsync(host.SendAsAsync(HttpMethod.Put, $"/people/{other}/password", PasswordBody(Another), host.Identifier, Token)),
        };

        Assert.Equal(3, answers.Count(status => status == HttpStatusCode.Forbidden));
        Assert.Equal(before, (await CountAsync(host, "public.person"), await CountAsync(host, "public.custody_event")));
    }

    [DatabaseFact]
    public async Task Ca2WithNoSessionTheThreeRoutesAnswerUnauthorizedAndNothingIsWritten()
    {
        await using var host = await AdministratorAsync();
        var before = await CountAsync(host, "public.custody_event");

        var answers = new[]
        {
            await StatusAsync(host.SendAsAsync(HttpMethod.Post, "/people", Person("zqx-new", Auditing, Secret), session: null, Token)),
            await StatusAsync(host.SendAsAsync(HttpMethod.Post, "/people/1/deactivate", "{}", session: null, Token)),
            await StatusAsync(host.SendAsAsync(HttpMethod.Put, "/people/1/password", PasswordBody(Another), session: null, Token)),
        };

        Assert.Equal(3, answers.Count(status => status == HttpStatusCode.Unauthorized));
        Assert.Equal((before, 1L), (await CountAsync(host, "public.custody_event"), await CountAsync(host, "public.person")));
    }

    [DatabaseFact]
    public async Task Ca3TheLastActiveAdministratorIsNotDeactivatedAndTheReasonIsGiven()
    {
        await using var host = await AdministratorAsync();
        var admin = await IdOfAsync(host, host.Name);
        var before = await CountAsync(host, "public.custody_event");

        using var refused = await host.PostAsync($"/people/{admin}/deactivate", "{}", Token);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Administração técnica", await refused.Content.ReadAsStringAsync(Token), StringComparison.Ordinal);
        Assert.Equal((1L, before), (await CountAsync(host, "public.person WHERE active"), await CountAsync(host, "public.custody_event")));
    }

    [DatabaseFact]
    public async Task Ca3WithAnotherAdministratorTheFirstMayBeDeactivatedAndThenTheLastIsKept()
    {
        await using var host = await AdministratorAsync();
        var first = await IdOfAsync(host, host.Name);
        using var made = await host.PostAsync("/people", Person("zqx-second-admin", PersonFunctions.Administration, Secret), Token);
        var second = await IdFromAsync(made);

        using var allowed = await host.PostAsync($"/people/{first}/deactivate", "{}", Token);
        var session = await host.EnterAsync("zqx-second-admin", Secret, Token);
        using var kept = await host.SendAsAsync(HttpMethod.Post, $"/people/{second}/deactivate", "{}", session, Token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict), (allowed.StatusCode, kept.StatusCode));
        Assert.Equal(1L, await CountAsync(host, "public.person WHERE active"));
    }

    [DatabaseFact]
    public async Task Ca4ThePersonDeactivatedLosesTheSessionAndTheEntryAndKeepsWhatTheySignedWithTheChainVerifying()
    {
        await using var host = await AdministratorAsync();
        using var made = await host.PostAsync("/people", Person("zqx-leaving", Auditing, Secret), Token);
        var id = await IdFromAsync(made);
        var session = await host.EnterAsync("zqx-leaving", Secret, Token);
        await using var source = NpgsqlDataSource.Create(host.ApplicationConnectionString);
        _ = await new CustodyChain(source).AppendAsync(
            CustodyEvent.Of(CustodyAuthor.Person(id), new CustodyItem(CustodyItemKind.ItemText, 7), CustodyStep.RawTextRead, CustodyReason.ContentRead), Token);
        using var before = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, session, Token);

        using var deactivated = await host.PostAsync($"/people/{id}/deactivate", "{}", Token);
        using var after = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, session, Token);
        var entry = await host.EnterAsync("zqx-leaving", Secret, Token);
        var verification = await new CustodyVerifier(source, TimeProvider.System).VerifyAsync(Token);

        Assert.NotNull(session);
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.OK, HttpStatusCode.Unauthorized), (before.StatusCode, deactivated.StatusCode, after.StatusCode));
        Assert.Null(entry);
        Assert.Equal(1L, await CountAsync(host, $"public.custody_event WHERE step = 'raw_text_read' AND item_id = 7 AND actor_person_id = {id}"));
        Assert.True(verification.Passed);
    }

    [DatabaseFact]
    public async Task Ca5ThePasswordSetAgainOpensOnTheNextEntryAndTheOldOneNoLonger()
    {
        await using var host = await AdministratorAsync();
        using var made = await host.PostAsync("/people", Person("zqx-resetting", Auditing, Secret), Token);
        var id = await IdFromAsync(made);
        var oldSession = await host.EnterAsync("zqx-resetting", Secret, Token);

        using var reset = await host.SendAsAsync(HttpMethod.Put, $"/people/{id}/password", PasswordBody(Another), host.Identifier, Token);
        var withNew = await host.EnterAsync("zqx-resetting", Another, Token);
        var withOld = await host.EnterAsync("zqx-resetting", Secret, Token);
        using var held = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, oldSession, Token);

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.NotNull(withNew);
        Assert.Null(withOld);
        Assert.Equal(HttpStatusCode.Unauthorized, held.StatusCode);
    }

    [DatabaseTheory]
    [InlineData("zqx-field", "Cozinheiro", Secret, 400, "função")]
    [InlineData("", Auditing, Secret, 400, "nome")]
    [InlineData("zqx-field", Auditing, "", 400, "senha")]
    [InlineData("zqx-admin-name", Auditing, Secret, 409, "nome")]
    public async Task Ca6AFieldNotAcceptedIsRefusedNamingItAndNothingIsWritten(string name, string function, string password, int status, string field)
    {
        await using var host = await AdministratorAsync();
        var theName = name == "zqx-admin-name" ? host.Name : name;
        var before = (await CountAsync(host, "public.person"), await CountAsync(host, "public.custody_event"));

        using var refused = await host.PostAsync("/people", Person(theName, function, password), Token);

        var body = await refused.Content.ReadAsStringAsync(Token);
        Assert.Equal(status, (int)refused.StatusCode);
        Assert.Equal(field, JsonDocument.Parse(body).RootElement.GetProperty("field").GetString());
        Assert.Equal(before, (await CountAsync(host, "public.person"), await CountAsync(host, "public.custody_event")));
    }

    [DatabaseFact]
    public async Task Ca6AnEmptyPasswordSetAgainIsRefusedNamingTheFieldAndThePasswordIsNeverAnswered()
    {
        await using var host = await AdministratorAsync();
        using var made = await host.PostAsync("/people", Person("zqx-quiet", Auditing, Secret), Token);
        var id = await IdFromAsync(made);

        using var refused = await host.SendAsAsync(HttpMethod.Put, $"/people/{id}/password", PasswordBody(string.Empty), host.Identifier, Token);

        var body = await refused.Content.ReadAsStringAsync(Token) + await made.Content.ReadAsStringAsync(Token);
        Assert.Equal((HttpStatusCode.BadRequest, "senha"), (refused.StatusCode, JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("field").GetString()));
        Assert.DoesNotContain(Secret, body, StringComparison.Ordinal);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<AuthenticatedHost> AdministratorAsync() =>
        AuthenticatedHost.StartAsync(new HostSetup { Function = PersonFunctions.Administration }, Token);

    private static string Person(string name, string function, string password) =>
        JsonSerializer.Serialize(new { name, function, password });

    private static string PasswordBody(string secret) => JsonSerializer.Serialize(new { password = secret });

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    private static async Task<long> IdFromAsync(HttpResponseMessage made) =>
        JsonDocument.Parse(await made.Content.ReadAsStringAsync(Token)).RootElement.GetProperty("id").GetInt64();

    private static async Task<long> IdOfAsync(AuthenticatedHost host, string name) =>
        (long)(await ScalarAsync(host, $"SELECT id FROM public.person WHERE name = '{name}'"))!;

    private static async Task<long> CreatedBySystemAsync(AuthenticatedHost host, string name)
    {
        await using var source = NpgsqlDataSource.Create(host.ApplicationConnectionString);
        var made = await new PersonRegistry(source).CreateAsync(new NewPerson(name, Auditing), CustodyAuthor.System, Token);
        return made.PersonId!.Value;
    }

    private static Task<long> EventsAsync(AuthenticatedHost host, string step, long item, long author) =>
        CountAsync(host, $"public.custody_event WHERE step = '{step}' AND item_kind = 'person' AND item_id = {item} AND actor_person_id = {author}");

    private static async Task<long> CountAsync(AuthenticatedHost host, string from) =>
        (long)(await ScalarAsync(host, $"SELECT count(*) FROM {from}"))!;

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> ScalarAsync(AuthenticatedHost host, string statement)
    {
        await using var connection = await Sql.OpenAsync(host.OwnerConnectionString);
        await using var command = new NpgsqlCommand(statement, connection);
        return await command.ExecuteScalarAsync(Token);
    }
}
