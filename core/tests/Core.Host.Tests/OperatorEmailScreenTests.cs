using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Core.Host.Tests;

/// <summary>
/// The page where the technical administration sees and corrects the e-mail address of another person (issue 635, part 3, decision 11 of the owner), rendered as the server renders it: it shows
/// the address kept in an optional field that posts to the page of that person, says the correction enters the chain without the address and that the person sees the new one on the account,
/// says what a saving did, gives a refusal at the field, and offers no form for a deactivated person; and the list of people leads to the page of each active person and of no other.
/// </summary>
public sealed class OperatorEmailScreenTests
{
    private const string Address = "zqx-pessoa@example.test";

    [Fact]
    public void TheAddressOfThePageOfAPersonCarriesItsIdentifier() => Assert.Equal("/pessoas/7/email", OperatorEmailEndpoint.Of(7));

    [Fact]
    public async Task AnActivePersonHasTheAddressKeptInAnOptionalFieldThatPostsToItsPage()
    {
        var page = await ShownAsync(new OperatorEmailScreen.Shown(7, "zqx-pessoa", Active: true, Address), saved: false, error: null);

        Assert.Contains("E-mail de zqx-pessoa", page, StringComparison.Ordinal);
        Assert.Contains("action=\"/pessoas/7/email\"", page, StringComparison.Ordinal);
        Assert.Contains($"value=\"{Address}\"", page, StringComparison.Ordinal);
        Assert.Contains("type=\"email\"", page, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-required", page, StringComparison.Ordinal);
        Assert.Contains("A correção fica registrada na cadeia de custódia sem o endereço, e a pessoa vê o endereço novo na própria conta.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"notice", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Address, "Endereço de e-mail de zqx-pessoa corrigido.")]
    [InlineData(null, "zqx-pessoa está sem endereço de e-mail.")]
    public async Task AfterACorrectionThePageSaysWhatItDid(string? address, string said)
    {
        var page = await ShownAsync(new OperatorEmailScreen.Shown(7, "zqx-pessoa", Active: true, address), saved: true, error: null);

        Assert.Contains(said, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARefusalIsToldAtTheFieldWithWhatWasTyped()
    {
        var page = await ShownAsync(new OperatorEmailScreen.Shown(7, "zqx-pessoa", Active: true, "zqx sem arroba"), saved: false, AccountEmailScreen.Invalid);

        Assert.Contains("value=\"zqx sem arroba\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", page, StringComparison.Ordinal);
        Assert.Contains(AccountEmailScreen.Invalid, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeactivatedPersonHasNoAddressAndNoForm()
    {
        var page = await ShownAsync(new OperatorEmailScreen.Shown(7, "zqx-pessoa", Active: false, Address: null), saved: false, error: null);

        Assert.Contains("Pessoa desativada, sem endereço.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<form", page, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"email\"", page, StringComparison.Ordinal);
        Assert.Contains("href=\"/pessoas\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheListLeadsToThePageOfTheAddressOfEachActivePersonAndOfNoOther()
    {
        ListedPerson[] listed = [new(3, "zqx-ativa", PersonFunctions.Accepted[0], Active: true), new(4, "zqx-inativa", PersonFunctions.Accepted[0], Active: false)];
        var people = new PersonPage(listed, 2, 1);

        var page = await RenderAsync(typeof(OperatorsScreen), new Dictionary<string, object?> { ["Page"] = people });

        Assert.Contains("href=\"/pessoas/3/email\"", page, StringComparison.Ordinal);
        Assert.Contains("E-mail de zqx-ativa", page, StringComparison.Ordinal);
        Assert.DoesNotContain("/pessoas/4/email", page, StringComparison.Ordinal);
        Assert.DoesNotContain("E-mail de zqx-inativa", page, StringComparison.Ordinal);
    }

    private static Task<string> ShownAsync(OperatorEmailScreen.Shown person, bool saved, string? error) =>
        RenderAsync(typeof(OperatorEmailScreen), new Dictionary<string, object?> { ["Subject"] = person, ["Saved"] = saved, ["Error"] = error });

    private static async Task<string> RenderAsync(Type screen, IDictionary<string, object?> given)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync(screen, ParameterView.FromDictionary(given))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
