using System.Net;
using System.Text.Json;
using Core.Host.Endpoints;
using Core.Persistence.People;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Core.Host.Tests;

/// <summary>
/// Nothing is reached by omission (policy 006, #814): every route the host maps behind the session declares who reaches it, JSON or page, and the routes of the seal and of the audit log
/// declare the functions their screens ask. The inventory is read from the routes the composition really maps, so a route added later with no declaration fails here.
/// </summary>
public sealed class ScreenAccessDeclarationTests
{
    [Fact]
    public void EveryMappedRouteDeclaresScreenAccess()
    {
        var undeclared = MappedRoutes.Read().Where(route => !route.Open && route.Access is null).Select(route => route.ToString()).Order(StringComparer.Ordinal);

        Assert.Empty(undeclared);
    }

    [Fact]
    public async Task SealRoutesRequireAdministration()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var declared = MappedRoutes.Read().Where(route => route.Pattern.StartsWith(SealEndpoint.Route, StringComparison.Ordinal)).ToList();
        await using var host = await StartAsync(SealEndpoint.Map, cancellationToken);
        var identifier = host.SeedSession();

        using var issue = await host.GetAsync(SealEndpoint.Route, identifier, encrypted: true, cancellationToken);
        using var confirmation = await host.PostAsync(SealEndpoint.ConfirmationRoute, JsonSerializer.Serialize(new { seal = "x" }), identifier, cancellationToken);

        Assert.Equal(2, declared.Count);
        Assert.All(declared, route => Assert.Equal([PersonFunctions.Administration], FunctionsOf(route)));
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (issue.StatusCode, confirmation.StatusCode));
    }

    [Fact]
    public async Task AuditLogRouteRequiresAdministrationOrAudit()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var declared = Assert.Single(MappedRoutes.Read(), route => route.Pattern == AuditEndpoint.Route);
        await using var host = await StartAsync(AuditEndpoint.Map, cancellationToken);

        using var listing = await host.GetAsync(AuditEndpoint.Route, host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal([PersonFunctions.Administration, PersonFunctions.Audit], FunctionsOf(declared));
        Assert.Equal(HttpStatusCode.NotFound, listing.StatusCode);
    }

    [Fact]
    public void SealRoutesDeclareTheAccessOfTheSealScreen()
    {
        var declared = MappedRoutes.Read().Where(route => route.Pattern.StartsWith(SealEndpoint.Route, StringComparison.Ordinal)).ToList();

        Assert.Equal(2, declared.Count);
        Assert.All(declared, route => Assert.Same(SealScreensEndpoint.Access, route.Access));
    }

    [Fact]
    public async Task ConfigurationAndVersionsRoutesRequireAdministrationOrAuditAsTheirScreensDo()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var configuration = Assert.Single(MappedRoutes.Read(), route => route.Pattern == ConfigurationEndpoint.Route);
        var versions = Assert.Single(MappedRoutes.Read(), route => route.Pattern == VersionsEndpoint.Route);
        await using var host = await StartAsync(routes =>
        {
            ConfigurationEndpoint.Map(routes);
            VersionsEndpoint.Map(routes);
        }, cancellationToken);
        var identifier = host.SeedSession();

        using var inForce = await host.GetAsync(ConfigurationEndpoint.Route, identifier, encrypted: true, cancellationToken);
        using var reported = await host.GetAsync(VersionsEndpoint.Route, identifier, encrypted: true, cancellationToken);

        Assert.Equal([PersonFunctions.Administration, PersonFunctions.Audit], FunctionsOf(configuration));
        Assert.Equal([PersonFunctions.Administration, PersonFunctions.Audit], FunctionsOf(versions));
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound), (inForce.StatusCode, reported.StatusCode));
    }

    [Fact]
    public async Task AnalysisRouteRequiresAdministrationBecauseItReadsTheContentOfAText()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var declared = Assert.Single(MappedRoutes.Read(), route => route.Pattern == AnalysisEndpoint.Route);
        await using var host = await StartAsync(AnalysisEndpoint.Map, cancellationToken);

        using var refused = await host.PostAsync(AnalysisEndpoint.Route, /*lang=json,strict*/ """{"text":"ZQX-FAKE-NOTHING","language":"pt"}""", host.SeedSession(), cancellationToken);

        Assert.Equal([PersonFunctions.Administration], FunctionsOf(declared));
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
    }

    [Fact]
    public async Task PeopleRoutesRequireAdministrationAndAnswer404NotForbiddenToWhoLacksIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var declared = MappedRoutes.Read().Where(route => route.Pattern.StartsWith(OperatorsEndpoint.Route, StringComparison.Ordinal)).ToList();
        await using var host = await StartAsync(OperatorsEndpoint.Map, cancellationToken);
        var identifier = host.SeedSession();

        using var made = await host.PostAsync(OperatorsEndpoint.Route, "{}", identifier, cancellationToken);
        using var deactivated = await host.PostAsync($"{OperatorsEndpoint.Route}/1/deactivate", "{}", identifier, cancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"{OperatorsEndpoint.Route}/1/password", UriKind.Relative))
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        using var reset = await host.SendAsync(request, identifier, encrypted: true, cancellationToken);

        Assert.Equal(3, declared.Count);
        Assert.All(declared, route => Assert.Equal([PersonFunctions.Administration], FunctionsOf(route)));
        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound), (made.StatusCode, deactivated.StatusCode, reset.StatusCode));
    }

    private static string[] FunctionsOf(MappedRoute route) => [.. (route.Access?.Functions ?? []).Order(StringComparer.Ordinal)];

    private static Task<SessionedHost> StartAsync(Action<IEndpointRouteBuilder> map, CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services,
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                map(routes);
            },
            cancellationToken);
}
