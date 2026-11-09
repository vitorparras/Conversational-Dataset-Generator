using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Core.Host.Tests;

/// <summary>
/// Every route the host maps asks for a session except the explicit exceptions (issue #495, CA-3): the inventory is read from the routes the composition
/// maps, so a route added later that answers with no session, and is not on the list, fails here. What the inventory cannot see, a branch of the pipeline
/// or a terminal middleware and who may mark a route open, is judged by the architecture rule.
/// </summary>
public sealed class SessionRoutesInventoryTests
{
    /// <summary>The only routes that answer with no session: the entry (page, form and JSON), the status, and the script and style of the screens the entry page needs.</summary>
    private static readonly string[] Exceptions = ["GET /entrada", "POST /entrada", "POST /session", "GET /status", "GET /screens/client.css", "GET /screens/client.js"];

    [Fact]
    public void OnlyTheEntryTheStatusAndTheStyleOfTheScreensAnswerWithNoSession()
    {
        var open = MappedRoutes.Read().Where(route => route.Open).Select(route => route.ToString());

        Assert.Equal(Exceptions.Order(StringComparer.Ordinal), open.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void EveryOtherRouteTheHostMapsIsBehindTheGate()
    {
        var gated = MappedRoutes.Read().Where(route => !route.Open).Select(route => route.ToString()).Order(StringComparer.Ordinal).ToList();

        string[] expected =
        [
            "DELETE /account/sessions/{id}", "DELETE /session", "GET /", "GET /account/sessions", "GET /audit/server", "GET /avisos", "GET /configuracao", "GET /configuration",
            "GET /custodia", "GET /custodia/verificar", "GET /custody/seal", "GET /custody/verification", "GET /dominios", "GET /estado-do-sistema", "GET /lotes",
            "GET /minha-conta", "GET /minha-conta/senha", "GET /minha-conta/sessoes/encerrar", "GET /minha-conta/sessoes/encerrar-outras", "GET /pessoas", "GET /pessoas/nova", "GET /session",
            "GET /versions",
        ];
        Assert.Equal(
            [
                .. expected,
                "POST /account/password",
                "POST /analysis",
                "POST /avisos/{id}/lido",
                "POST /custodia/verificar",
                "POST /custody/seal/confirmation",
                "POST /custody/verification",
                "POST /minha-conta/senha",
                "POST /minha-conta/sessoes/encerrar",
                "POST /minha-conta/sessoes/encerrar-outras",
                "POST /people",
                "POST /people/{id}/deactivate",
                "POST /pessoas/nova",
                "POST /saida",
                "PUT /people/{id}/password",
            ],
            gated);
    }

    [Fact]
    public void NoRouteIsShortCircuitedPastTheGate()
    {
        var skipping = MappedRoutes.Read().Where(route => route.ShortCircuited).Select(route => route.ToString());

        Assert.Empty(skipping);
    }

    [Fact]
    public void ShortCircuitedRouteIsSeenByTheInventory()
    {
        var seen = MappedRoutes.Read(app => _ = app.MapGet("/zqx-skipping-7731", () => Results.NoContent()).ShortCircuit());

        var skipping = Assert.Single(seen, route => route.ShortCircuited);
        Assert.Equal("GET /zqx-skipping-7731", skipping.ToString());
    }
}
