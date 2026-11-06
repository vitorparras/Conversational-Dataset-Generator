using Core.Host.Screens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Core.Tests.Browser.Trial;

/// <summary>
/// The screens that exist for the test alone: the bare page and the catalog of the components. Nothing of production serves them; the host of the test maps them beside the
/// routes of the screens.
/// </summary>
internal static class TrialRoutes
{
    public const string Bare = "/ensaio/base";
    public const string Catalog = "/ensaio/catalogo";

    public static void Map(IEndpointRouteBuilder routes)
    {
        _ = routes.MapGet(Bare, () => ScreenResult.Of(typeof(BareScreen), "Tela base"));
        _ = routes.MapGet(Catalog, () => ScreenResult.Of(typeof(CatalogScreen), "Catálogo dos componentes"));
    }
}
