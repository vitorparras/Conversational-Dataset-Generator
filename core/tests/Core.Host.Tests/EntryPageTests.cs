using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;

namespace Core.Host.Tests;

/// <summary>
/// The entry and the exit as a browser walks them (#592 and #593), in process and over a real database: the form of the entry, the same refusal for a name nobody goes by and a wrong
/// password, the name given back and the password never, the registry empty said and unsaid, the exit that ends the session on the server, and no password in an address, a log or an answer.
/// </summary>
public sealed class EntryPageTests
{
    private const string Refusal = "Nome ou senha não conferem";
    private const string CookieName = "__Host-sessao";
    private const string BrowserAccept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    [Fact]
    public async Task TheEntryPageIsServedWithNoSessionAsAWholePageThatNothingKeeps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var entry = await EntryPagesHost.StartAsync(withPerson: true, cancellationToken);

        using var response = await entry.Host.SendAsync(Request(HttpMethod.Get, ScreenPaths.SignIn), identifier: null, encrypted: true, cancellationToken);
        var page = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal((HttpStatusCode.OK, "no-store"), (response.StatusCode, response.Headers.CacheControl?.ToString()));
        Assert.Contains($"action=\"{ScreenPaths.SignIn}\"", page, StringComparison.Ordinal);
        Assert.Contains($"name=\"{SignInScreen.NameField}\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain(Refusal, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANameNobodyGoesByAndAWrongPasswordGetTheSameRefusalWithTheNameBackAndNoPassword()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var entry = await EntryPagesHost.StartAsync(withPerson: true, cancellationToken);
        var stranger = $"zqx-ninguem-{Guid.NewGuid():N}";
        var wrong = $"zqx-errada-{Guid.NewGuid():N}";

        var unknown = await EnterAsync(entry, stranger, wrong, cancellationToken);
        var misspelled = await EnterAsync(entry, entry.Name, wrong, cancellationToken);

        Assert.All([unknown, misspelled], answer => Assert.Equal(HttpStatusCode.OK, answer.Status));
        Assert.All([unknown, misspelled], answer => Assert.Contains(Refusal, answer.Page, StringComparison.Ordinal));
        Assert.Contains($"value=\"{stranger}\"", unknown.Page, StringComparison.Ordinal);
        Assert.Contains($"value=\"{entry.Name}\"", misspelled.Page, StringComparison.Ordinal);
        Assert.Equal(unknown.Page.Replace(stranger, "NOME", StringComparison.Ordinal), misspelled.Page.Replace(entry.Name, "NOME", StringComparison.Ordinal));
        Assert.DoesNotContain(wrong, unknown.Page + misspelled.Page, StringComparison.Ordinal);
        Assert.Empty(unknown.Cookies.Concat(misspelled.Cookies));
    }

    [Fact]
    public async Task TheRegistryEmptyIsSaidOnTheEntryAndIsNotSaidOnceThereIsAPerson()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var empty = await EntryPagesHost.StartAsync(withPerson: false, cancellationToken);
        await using var filled = await EntryPagesHost.StartAsync(withPerson: true, cancellationToken);

        var before = await PageAsync(empty, cancellationToken);
        var after = await PageAsync(filled, cancellationToken);

        Assert.Contains("cadastro vazio", before, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cadastro vazio", after, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheRightNameAndPasswordOpenASessionAndTakeTheBrowserToTheHomeWithTheExitInTheHeader()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var entry = await EntryPagesHost.StartAsync(withPerson: true, cancellationToken);

        var entered = await EnterAsync(entry, entry.Name, entry.Password, cancellationToken);
        var identifier = Identifier(entered);
        using var home = await entry.Host.SendAsync(Request(HttpMethod.Get, ScreenPaths.Home), identifier, encrypted: true, cancellationToken);
        var page = await home.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.Home), (entered.Status, entered.Location));
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains($"action=\"{ScreenPaths.SignOut}\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheExitEndsTheSessionOnTheServerAndTheIdentifierPresentedAgainIsAnAnswerWithNoSession()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var entry = await EntryPagesHost.StartAsync(withPerson: true, cancellationToken);
        var identifier = Identifier(await EnterAsync(entry, entry.Name, entry.Password, cancellationToken));
        using var probe = await entry.Host.GetAsync(SessionEndpoint.Route, identifier, encrypted: true, cancellationToken);

        using var left = await entry.Host.SendAsync(Request(HttpMethod.Post, ScreenPaths.SignOut), identifier, encrypted: true, cancellationToken);
        using var again = await entry.Host.SendAsync(Request(HttpMethod.Get, ScreenPaths.Home), identifier, encrypted: true, cancellationToken);
        using var json = await entry.Host.GetAsync(SessionEndpoint.Route, identifier, encrypted: true, cancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, probe.StatusCode);
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.SignIn), (left.StatusCode, left.Headers.Location?.OriginalString));
        Assert.Contains(left.Headers.GetValues("Set-Cookie"), line => line.StartsWith($"{CookieName}=;", StringComparison.Ordinal));
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.SignIn), (again.StatusCode, again.Headers.Location?.OriginalString));
        Assert.Equal(HttpStatusCode.Unauthorized, json.StatusCode);
    }

    [Fact]
    public async Task ThePasswordIsInNoAddressNoAnswerAndNoLineOfTheLog()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var entry = await EntryPagesHost.StartAsync(withPerson: true, cancellationToken);
        var wrong = $"zqx-errada-{Guid.NewGuid():N}";

        var refused = await EnterAsync(entry, entry.Name, wrong, cancellationToken);
        var accepted = await EnterAsync(entry, entry.Name, entry.Password, cancellationToken);
        var seen = string.Concat(refused.Everything, accepted.Everything, string.Join('\n', entry.Log.Lines));

        Assert.DoesNotContain(wrong, seen, StringComparison.Ordinal);
        Assert.DoesNotContain(entry.Password, seen, StringComparison.Ordinal);
        Assert.NotEmpty(entry.Log.Lines);
    }

    private static HttpRequestMessage Request(HttpMethod method, string route)
    {
        var request = new HttpRequestMessage(method, new Uri(route, UriKind.Relative));
        request.Headers.Accept.ParseAdd(BrowserAccept);
        return request;
    }

    private static string Identifier(Answer entered)
    {
        var line = Assert.Single(entered.Cookies, cookie => cookie.StartsWith($"{CookieName}=", StringComparison.Ordinal));
        return line[(CookieName.Length + 1)..line.IndexOf(';', StringComparison.Ordinal)];
    }

    private static async Task<string> PageAsync(EntryPagesHost entry, CancellationToken cancellationToken)
    {
        using var response = await entry.Host.SendAsync(Request(HttpMethod.Get, ScreenPaths.SignIn), identifier: null, encrypted: true, cancellationToken);
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>The form of the entry sent as a browser sends it: the name and the password in the body, as a form.</summary>
    private static async Task<Answer> EnterAsync(EntryPagesHost entry, string name, string password, CancellationToken cancellationToken)
    {
        var form = Request(HttpMethod.Post, ScreenPaths.SignIn);
        form.Content = new FormUrlEncodedContent([KeyValuePair.Create(SignInScreen.NameField, name), KeyValuePair.Create(SignInScreen.SecretField, password)]);
        using var response = await entry.Host.SendAsync(form, identifier: null, encrypted: true, cancellationToken);
        return new Answer(
            response.StatusCode,
            response.Headers.Location?.OriginalString,
            await response.Content.ReadAsStringAsync(cancellationToken),
            response.Headers.TryGetValues("Set-Cookie", out var cookies) ? [.. cookies] : [],
            response.Headers.ToString());
    }

    private sealed record Answer(HttpStatusCode Status, string? Location, string Page, IReadOnlyList<string> Cookies, string Headers)
    {
        public string Everything => $"{Page}\n{Headers}";
    }
}
