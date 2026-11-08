using System.Net;
using System.Text.RegularExpressions;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Persistence.Content.Emptying;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>
/// The routes of the emptying of an item (#617) behind the real gates, over a database of their own: only the curation of the collection with a domain granted reaches them, and everyone else gets
/// the answer of a route that does not exist (CA-5); the confirmation is a page of its own and the act is a POST that ends in a 303 (decision 0051); a refusal comes back on the form.
/// </summary>
[Trait("Category", "Database")]
public sealed partial class EmptyingRoutesTests
{
    private const string Browser = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";
    private const string Protocol = "PROT-2026/0042";
    private const string Missing = "/zqx-nao-existe-6170";
    private const string Removal = "pedido-de-remocao";

    [DatabaseTheory]
    [InlineData(ScreenPaths.Emptying)]
    [InlineData(ScreenPaths.EmptyingConfirm)]
    public async Task OnlyTheCurationWithADomainReachesTheScreensAndEveryoneElseGetsTheAnswerOfARouteThatDoesNotExist(string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        foreach (var person in new[] { world.Annotator, world.Auditor })
        {
            var session = host.Sessions.Open(person);
            var (missingStatus, missingBody) = await AnswerOfAsync(host, Missing, session, cancellationToken);
            var (status, body) = await AnswerOfAsync(host, $"{route}?item=1", session, cancellationToken);
            Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, WithoutAddress(missingBody)), (missingStatus, status, WithoutAddress(body)));
        }
    }

    [DatabaseFact]
    public async Task ThePostOfAPersonWithoutTheFunctionIsAnsweredAsARouteThatDoesNotExistAndNothingIsEmptied()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        using var refused = await host.SendAsync(Post(("item", "1"), ("motivo", Removal), ("protocolo", Protocol)), host.Sessions.Open(world.Annotator), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.NotFound, null), (refused.StatusCode, await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, cancellationToken)));
    }

    [DatabaseFact]
    public async Task TheEntryAsksOnlyForTheIdentifierAndTheItemItShowsHasNoTextAndNoCountOfMaps()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var session = host.Sessions.Open(world.Curator);

        var entry = await PageOfAsync(host, ScreenPaths.Emptying, session, cancellationToken);
        var item = await PageOfAsync(host, $"{ScreenPaths.Emptying}?item=1", session, cancellationToken);

        Assert.Contains("Identificador do item", entry, StringComparison.Ordinal);
        Assert.Contains("Esvaziar item", entry, StringComparison.Ordinal);
        Assert.Contains("Item 1", item, StringComparison.Ordinal);
        Assert.Contains("Sem esvaziamento registrado", item, StringComparison.Ordinal);
        Assert.Contains("e os mapas de reversão que tiver", item, StringComparison.Ordinal);
        Assert.DoesNotContain("zqx", item.Replace("zqx-domain-a", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task AnIdentifierThatIsNotAPositiveNumberIsRefusedOnTheForm(string typed)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        var page = await PageOfAsync(host, $"{ScreenPaths.Emptying}?item={typed}", host.Sessions.Open(world.Curator), cancellationToken);

        Assert.Contains("O identificador é um número inteiro maior que zero.", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task TheConfirmationIsAPageOfItsOwnAndAnIdentifierThatIsNotANumberGoesBackToTheEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var session = host.Sessions.Open(world.Curator);

        var confirmation = await PageOfAsync(host, $"{ScreenPaths.EmptyingConfirm}?item=1", session, cancellationToken);
        using var back = await host.SendAsync(Get($"{ScreenPaths.EmptyingConfirm}?item=abc"), session, encrypted: true, cancellationToken);

        Assert.Contains("Confirmar o esvaziamento", confirmation, StringComparison.Ordinal);
        Assert.Contains("Esvaziar o item 1", confirmation, StringComparison.Ordinal);
        Assert.Equal((HttpStatusCode.SeeOther, "/esvaziamento?item=abc"), (back.StatusCode, back.Headers.Location?.OriginalString));
    }

    [DatabaseFact]
    public async Task TheActEmptiesTheItemEndsInA303AndTheEntryThenShowsTheRecordWithTheProtocolAndNotTheNameOfWhoAsked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var session = host.Sessions.Open(world.Curator);

        using var done = await host.SendAsync(Post(("item", "1"), ("motivo", Removal), ("protocolo", Protocol)), session, encrypted: true, cancellationToken);
        var shown = await PageOfAsync(host, done.Headers.Location!.OriginalString, session, cancellationToken);

        Assert.Equal((HttpStatusCode.SeeOther, "/esvaziamento?item=1&feito=1"), (done.StatusCode, done.Headers.Location?.OriginalString));
        Assert.Contains("O item foi esvaziado.", shown, StringComparison.Ordinal);
        Assert.Contains("zqx-curator", shown, StringComparison.Ordinal);
        Assert.Contains(Protocol, shown, StringComparison.Ordinal);
        Assert.Equal(EmptyingMotive.RemovalRequestedBySubject, (await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, cancellationToken))?.Motive);
    }

    [DatabaseFact]
    public async Task AnItemAlreadyEmptiedIsToldAsAlreadyEmptiedAndItsConfirmationGoesBackToTheEntry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var session = host.Sessions.Open(world.Curator);
        _ = await world.EmptyAsync(world.Curator, EmptyingMotive.ContractRequirement, Protocol);

        using var again = await host.SendAsync(Post(("item", "1"), ("motivo", Removal), ("protocolo", "OUTRO-1")), session, encrypted: true, cancellationToken);
        using var confirmation = await host.SendAsync(Get($"{ScreenPaths.EmptyingConfirm}?item=1"), session, encrypted: true, cancellationToken);
        var shown = await PageOfAsync(host, "/esvaziamento?item=1", session, cancellationToken);

        Assert.Equal(("/esvaziamento?item=1", "/esvaziamento?item=1"), (again.Headers.Location?.OriginalString, confirmation.Headers.Location?.OriginalString));
        Assert.Contains("Este item já está esvaziado.", shown, StringComparison.Ordinal);
        Assert.Contains(Protocol, shown, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [InlineData("", Protocol, "Escolha o motivo do esvaziamento.")]
    [InlineData("motivo-que-nao-existe", Protocol, "Escolha o motivo do esvaziamento.")]
    [InlineData(Removal, "Maria da Silva", "O protocolo aceita só letras, dígitos e . _ / -, até 64 caracteres.")]
    [InlineData(Removal, "", "O protocolo aceita só letras, dígitos e . _ / -, até 64 caracteres.")]
    public async Task AFormWithoutAMotiveOrWithAProtocolThatIsNotANumberComesBackOnTheConfirmationAndNothingIsEmptied(string motive, string protocol, string refusal)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        using var answer = await host.SendAsync(Post(("item", "1"), ("motivo", motive), ("protocolo", protocol)), host.Sessions.Open(world.Curator), encrypted: true, cancellationToken);
        var page = WebUtility.HtmlDecode(await answer.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
        Assert.Contains(refusal, page, StringComparison.Ordinal);
        Assert.Null(await world.Desk.RecordOfAsync(1, EmptyingWorld.Item, cancellationToken));
    }

    [DatabaseFact]
    public async Task AnItemThatIsNotThereIsAnsweredByTheEntryFormItselfWithTheRefusalOnTheField()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var session = host.Sessions.Open(world.Curator);

        using var answer = await host.SendAsync(Post(("item", "999"), ("motivo", Removal), ("protocolo", Protocol)), session, encrypted: true, cancellationToken);
        var shown = WebUtility.HtmlDecode(await answer.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal((HttpStatusCode.OK, null), (answer.StatusCode, answer.Headers.Location));
        Assert.Contains("Nenhum item do domínio tem esse identificador.", shown, StringComparison.Ordinal);
        Assert.Contains("value=\"999\"", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("Seguir para o esvaziamento", shown, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AForgedAddressNeverMakesTheEntrySayThatAnItemThatExistsDoesNot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await EmptyingWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var session = host.Sessions.Open(world.Curator);

        var forged = await PageOfAsync(host, $"/esvaziamento?item={EmptyingWorld.Item}&ausente=1", session, cancellationToken);
        var plain = await PageOfAsync(host, $"/esvaziamento?item={EmptyingWorld.Item}", session, cancellationToken);

        Assert.DoesNotContain("Nenhum item do domínio tem esse identificador.", forged, StringComparison.Ordinal);
        Assert.Equal(WithoutAction(plain), WithoutAction(forged));
    }

    private static async Task<string> PageOfAsync(SessionedHost host, string route, string session, CancellationToken cancellationToken)
    {
        using var response = await host.SendAsync(Get(route), session, encrypted: true, cancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(cancellationToken));
    }

    /// <summary>The page with the address the domain selector sends back to, which is the one the person asked and says nothing of the route.</summary>
    private static string WithoutAddress(byte[] page) => AddressOfTheSelector().Replace(System.Text.Encoding.UTF8.GetString(page), string.Empty);

    private static string WithoutAction(string page) => AddressOfTheSelector().Replace(page, string.Empty);

    [GeneratedRegex("action=\"[^\"]*\"", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AddressOfTheSelector();

    private static async Task<(HttpStatusCode Status, byte[] Body)> AnswerOfAsync(SessionedHost host, string route, string session, CancellationToken cancellationToken)
    {
        using var response = await host.SendAsync(Get(route), session, encrypted: true, cancellationToken);
        return (response.StatusCode, await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    private static HttpRequestMessage Get(string route)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        request.Headers.Accept.ParseAdd(Browser);
        return request;
    }

    private static HttpRequestMessage Post(params (string Name, string Value)[] fields)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ScreenPaths.EmptyingConfirm, UriKind.Relative))
        {
            Content = new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))),
        };
        request.Headers.Accept.ParseAdd(Browser);
        return request;
    }

    private static Task<SessionedHost> StartAsync(EmptyingWorld world, CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services
                .AddSingleton(new ServiceCollection().AddSingleton(world.Source).BuildServiceProvider())
                .AddSingleton(provider => new OperatorAdministration(
                    new PersonRegistry(world.Source), new PersonCredentials(world.Source, new PasswordDerivation(1)), provider.GetRequiredService<SessionStore>()))
                .AddSingleton<DomainGrants>(new GrantedTo(world.Curator, world.Annotator)),
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                EmptyingScreensEndpoint.Map(routes);
            },
            cancellationToken);

    /// <summary>The source of grants of the test: the first domain to the people it was filled with and none to anyone else.</summary>
    private sealed class GrantedTo : DomainGrants
    {
        private readonly long[] people;

        internal GrantedTo(params long[] people)
        {
            this.people = people;
        }

        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult(people.Contains(entered) ? Enumerable.Repeat("zqx-domain-a", 1) : None);
    }
}
