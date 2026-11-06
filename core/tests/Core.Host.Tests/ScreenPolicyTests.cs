using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>
/// The question the route and the menu ask of the policy (#594, CA-3): nothing is reached by omission, the administration and the audit reach their screens by the
/// function alone, the other functions only with a domain granted, and the menu lists what the same question lets through, in the place each screen declares.
/// </summary>
public sealed class ScreenPolicyTests
{
    [Fact]
    public void ARouteWithNoMarkIsReachedByNoOneAndAPageOfEveryoneByAnyone()
    {
        var audit = new ScreenReach(PersonFunctions.Audit, DomainGrants.None);

        Assert.Equal((false, true, true), (audit.Reaches(null), audit.Reaches(ScreenAccess.Everyone), ScreenReach.Nothing.Reaches(ScreenAccess.Everyone)));
    }

    [Theory]
    [InlineData("Administração técnica", true)]
    [InlineData("Auditoria", true)]
    [InlineData("Anotação", false)]
    [InlineData("Arbitragem", false)]
    [InlineData("Curadoria do acervo", false)]
    [InlineData("Avaliação", false)]
    public void WithNoDomainGrantedOnlyTheFunctionsWithNoAxisOfDomainReachTheirScreen(string function, bool reached)
    {
        var reach = new ScreenReach(function, DomainGrants.None);

        Assert.Equal(reached, reach.Reaches(ScreenAccess.Of("Tela", 1, function)));
    }

    [Fact]
    public void WithADomainGrantedAFunctionWithAnAxisOfDomainReachesItsScreenAndNoOtherFunctionsOne()
    {
        var reach = new ScreenReach("Anotação", Enumerable.Repeat("zqx-dominio", 1));

        Assert.Equal((true, false), (reach.Reaches(ScreenAccess.Of("Anotar", 1, "Anotação")), reach.Reaches(ScreenAccess.Of("Auditar", 2, PersonFunctions.Audit))));
    }

    [Fact]
    public void TheMenuListsWhatThePolicyLetsThroughInThePlaceEachScreenDeclaresNotTheOrderOfMapping()
    {
        var context = ContextWith(
            Screen("/zqx-segunda", ScreenAccess.Of("Segunda", 2, PersonFunctions.Audit)),
            Screen("/zqx-alheia", ScreenAccess.Of("Alheia", 0, PersonFunctions.Administration)),
            Screen("/zqx-primeira", ScreenAccess.Of("Primeira", 1, PersonFunctions.Audit, PersonFunctions.Administration)),
            Screen("/zqx-de-todos", ScreenAccess.Everyone),
            Screen("/zqx-sem-marca", access: null));

        var menu = ScreenMenu.Of(context, new ScreenReach(PersonFunctions.Audit, DomainGrants.None));

        Assert.Equal(["Primeira:/zqx-primeira", "Segunda:/zqx-segunda"], menu.Select(entry => $"{entry.Caption}:{entry.Path}"));
    }

    private static DefaultHttpContext ContextWith(params Endpoint[] endpoints)
    {
        var services = new ServiceCollection().AddSingleton<EndpointDataSource>(new DefaultEndpointDataSource(endpoints)).BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = services };
    }

    private static RouteEndpoint Screen(string route, ScreenAccess? access) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse(route), 0, access is null ? EndpointMetadataCollection.Empty : new EndpointMetadataCollection(access), route);
}
