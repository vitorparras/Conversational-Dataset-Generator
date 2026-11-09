using System.Net;
using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The e-mail address of the own account and of the registration (issue 635, CA-4), with no database reached: a text that is no address is refused at its field, with 400, before the registry is
/// asked; with no database declared the page says the host cannot; and both forms offer the address as an optional field, which the account says only the person and the administration see.
/// </summary>
public sealed class AccountEmailRouteTests
{
    private const string Browser = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WhatIsNoAddressIsRefusedAtItsFieldBeforeTheRegistryIsAsked()
    {
        await using var nowhere = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=zqx;Username=zqx;Timeout=1");
        await using var host = await SessionedHost.StartAsync(services => services.AddSingleton(new PersonEmails(nowhere)), AccountEmailEndpoint.Map, Token);

        using var answer = await host.SendAsync(Post("zqx sem arroba"), host.Sessions.Open(7), encrypted: true, Token);
        var page = WebUtility.HtmlDecode(await answer.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.BadRequest, answer.StatusCode);
        Assert.Contains(AccountEmailScreen.Invalid, page, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithNoDatabaseDeclaredThePageSaysTheHostCannot()
    {
        await using var host = await SessionedHost.StartAsync(services => services, AccountEmailEndpoint.Map, Token);

        using var shown = await host.GetAsync(ScreenPaths.AccountEmail, host.Sessions.Open(7), encrypted: true, Token);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, shown.StatusCode);
    }

    [Theory]
    [InlineData(null, false, null)]
    [InlineData("zqx@example.test", true, "Endereço de e-mail guardado.")]
    [InlineData(null, true, "Você está sem endereço de e-mail.")]
    public async Task ThePageOfTheAccountOffersTheAddressAsAnOptionalFieldAndSaysWhatTheSavingDid(string? address, bool saved, string? said)
    {
        var page = await RenderAsync<AccountEmailScreen>(new Dictionary<string, object?> { ["Address"] = address, ["Saved"] = saved });

        Assert.Contains("type=\"email\"", page, StringComparison.Ordinal);
        Assert.Contains("Só você e a administração técnica o veem.", page, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-required", page, StringComparison.Ordinal);
        Assert.Equal(said is not null, said is not null && page.Contains(said, StringComparison.Ordinal));
        Assert.Equal(address is not null, address is not null && page.Contains($"value=\"{address}\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheRegistrationOffersTheAddressAsAnOptionalFieldAndGivesItBackWithARefusal()
    {
        var page = await RenderAsync<OperatorFormScreen>(new Dictionary<string, object?>
        {
            ["Email"] = "zqx sem arroba", ["ErrorField"] = AccountEmailScreen.EmailField, ["ErrorReason"] = AccountEmailScreen.Invalid,
        });

        Assert.Contains("id=\"pessoa-email\"", page, StringComparison.Ordinal);
        Assert.Contains("value=\"zqx sem arroba\"", page, StringComparison.Ordinal);
        Assert.Contains($"id=\"pessoa-email-erro\" class=\"field__error\" role=\"alert\">{AccountEmailScreen.Invalid}", page, StringComparison.Ordinal);
    }

    private static HttpRequestMessage Post(string typed)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ScreenPaths.AccountEmail, UriKind.Relative))
        {
            Content = new FormUrlEncodedContent([KeyValuePair.Create(AccountEmailScreen.EmailField, typed)]),
        };
        request.Headers.Accept.ParseAdd(Browser);
        return request;
    }

    private static async Task<string> RenderAsync<TScreen>(IDictionary<string, object?> given)
        where TScreen : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<TScreen>(ParameterView.FromDictionary(given))).ToHtmlString());
        return WebUtility.HtmlDecode(html);
    }
}
