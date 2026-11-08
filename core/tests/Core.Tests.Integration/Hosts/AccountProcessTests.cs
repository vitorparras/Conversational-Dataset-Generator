using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Core.Persistence.Tests.Fixtures;

namespace Core.Tests.Integration.Hosts;

/// <summary>
/// The sessions and the own password of the person of the session (issues 597 and 598), on the built host over a database of its own: the list names the sessions by an identifier that is not the
/// session's, ending one makes the server refuse it at once, the change checks the current password, counts a refusal in the delay of the entry, ends the other sessions, keeps the one that
/// asked, and the event says who and how many, never a password. Only the status, the body and the rows are looked at.
/// </summary>
[Trait("Category", "Database")]
public sealed class AccountProcessTests
{
    private const string Replacement = "ZQX-replacement-secret-5521";
    private const string Wrong = "ZQX-wrong-secret-9042";
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMilliseconds(900);

    [DatabaseFact]
    public async Task TheListHoldsTheSessionsOfThePersonWithTheCurrentOneMarkedAndNeitherTheSessionIdentifierNorTheNetwork()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);
        var other = await host.EnterAsync(host.Name, host.Password, Token);

        using var listed = await host.GetAsync("/account/sessions", Token);

        var body = await listed.Content.ReadAsStringAsync(Token);
        var items = JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items.Count(item => item.GetProperty("current").GetBoolean()));
        Assert.All(items, item => Assert.Equal(["current", "id", "lastActivity", "start"], item.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)));
        Assert.DoesNotContain(host.Identifier, body, StringComparison.Ordinal);
        Assert.DoesNotContain(other!, body, StringComparison.Ordinal);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task EndingASessionByItsListingIdentifierMakesTheServerRefuseItAtOnceAndLeavesTheOthers()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);
        var other = await host.EnterAsync(host.Name, host.Password, Token);
        var id = await OtherListingAsync(host);

        using var ended = await host.SendAsAsync(HttpMethod.Delete, $"/account/sessions/{id}", json: null, host.Identifier, Token);
        using var refused = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, other, Token);
        using var kept = await host.GetAsync("/session", Token);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.Unauthorized, HttpStatusCode.NoContent), (ended.StatusCode, refused.StatusCode, kept.StatusCode));
    }

    [DatabaseFact]
    public async Task TheSessionIdentifierEndsNothingWhenGivenInPlaceOfTheListingOne()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);
        var other = await host.EnterAsync(host.Name, host.Password, Token);

        using var missing = await host.SendAsAsync(HttpMethod.Delete, $"/account/sessions/{other}", json: null, host.Identifier, Token);
        using var held = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, other, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NoContent), (missing.StatusCode, held.StatusCode));
    }

    [DatabaseFact]
    public async Task WithNoSessionTheThreeRoutesAnswerUnauthorizedAndNothingIsWritten()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);
        var before = await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event", Token);

        var answers = new[]
        {
            await StatusAsync(host.SendAsAsync(HttpMethod.Get, "/account/sessions", json: null, session: null, Token)),
            await StatusAsync(host.SendAsAsync(HttpMethod.Delete, "/account/sessions/abc", json: null, session: null, Token)),
            await StatusAsync(host.SendAsAsync(HttpMethod.Post, "/account/password", ChangeBody(host.Password, Replacement), session: null, Token)),
        };

        Assert.Equal(3, answers.Count(status => status == HttpStatusCode.Unauthorized));
        Assert.Equal(before, await host.NumberAsSuperuserAsync("SELECT count(*) FROM public.custody_event", Token));
    }

    [DatabaseFact]
    public async Task ARightCurrentPasswordChangesItEndsTheOtherSessionsKeepsTheOneThatAskedAndTheEventSaysHowMany()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);
        var second = await host.EnterAsync(host.Name, host.Password, Token);
        var third = await host.EnterAsync(host.Name, host.Password, Token);
        var person = await host.NumberAsSuperuserAsync($"SELECT id FROM public.person WHERE name = '{host.Name}'", Token);

        using var changed = await host.PostAsync("/account/password", ChangeBody(host.Password, Replacement), Token);

        var body = await changed.Content.ReadAsStringAsync(Token);
        using var secondAfter = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, second, Token);
        using var thirdAfter = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, third, Token);
        using var current = await host.GetAsync("/session", Token);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(2, JsonDocument.Parse(body).RootElement.GetProperty("sessionsEnded").GetInt32());
        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.NoContent), (secondAfter.StatusCode, thirdAfter.StatusCode, current.StatusCode));
        Assert.NotNull(await host.EnterAsync(host.Name, Replacement, Token));
        Assert.Null(await host.EnterAsync(host.Name, host.Password, Token));
        Assert.Equal(1L, await host.NumberAsSuperuserAsync(Events(person, reference: "2"), Token));
        Assert.DoesNotContain(Replacement, body, StringComparison.Ordinal);
        Assert.DoesNotContain(host.Password, body, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AWrongCurrentPasswordIsRefusedWithTheSameTextChangesNothingEndsNoSessionAndCountsInTheDelayOfTheEntry()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);
        var other = await host.EnterAsync(host.Name, host.Password, Token);
        var person = await host.NumberAsSuperuserAsync($"SELECT id FROM public.person WHERE name = '{host.Name}'", Token);

        using var refused = await host.PostAsync("/account/password", ChangeBody(Wrong, Replacement), Token);
        var watch = Stopwatch.StartNew();
        var entry = await host.EnterAsync(host.Name, Wrong, Token);
        watch.Stop();

        var answer = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token)).RootElement;
        using var held = await host.SendAsAsync(HttpMethod.Get, "/session", json: null, other, Token);
        Assert.Equal((HttpStatusCode.BadRequest, "senha-atual"), (refused.StatusCode, answer.GetProperty("field").GetString()));
        Assert.Equal("A senha atual não confere.", answer.GetProperty("reason").GetString());
        Assert.Null(entry);
        Assert.True(watch.Elapsed >= FirstDelay, $"the refusal of the entry after a refused change waited only {watch.Elapsed}");
        Assert.Equal(HttpStatusCode.NoContent, held.StatusCode);
        Assert.Equal(0L, await host.NumberAsSuperuserAsync(Events(person, reference: null), Token));
        Assert.NotNull(await host.EnterAsync(host.Name, host.Password, Token));
    }

    [DatabaseFact]
    public async Task AnEmptyNewPasswordIsRefusedNamingTheFieldAndNothingIsChecked()
    {
        await using var host = await AuthenticatedHost.StartAsync(new HostSetup(), Token);

        using var refused = await host.PostAsync("/account/password", ChangeBody(host.Password, string.Empty), Token);

        var answer = JsonDocument.Parse(await refused.Content.ReadAsStringAsync(Token)).RootElement;
        Assert.Equal((HttpStatusCode.BadRequest, "senha-nova"), (refused.StatusCode, answer.GetProperty("field").GetString()));
        Assert.NotNull(await host.EnterAsync(host.Name, host.Password, Token));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string ChangeBody(string current, string replacement) => JsonSerializer.Serialize(new { current, replacement });

    private static string Events(long person, string? reference) =>
        $"SELECT count(*) FROM public.custody_event WHERE step = 'password_changed' AND item_kind = 'person' AND item_id = {person} AND actor_person_id = {person}"
        + (reference is null ? string.Empty : $" AND reference = '{reference}'");

    private static async Task<HttpStatusCode> StatusAsync(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        return response.StatusCode;
    }

    /// <summary>The listing identifier of the one session that is not the current.</summary>
    private static async Task<string> OtherListingAsync(AuthenticatedHost host)
    {
        using var listed = await host.GetAsync("/account/sessions", Token);
        var items = JsonDocument.Parse(await listed.Content.ReadAsStringAsync(Token)).RootElement.EnumerateArray();
        return items.Single(item => !item.GetProperty("current").GetBoolean()).GetProperty("id").GetString()!;
    }
}
