using Core.Host.Screens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Core.Tests.Browser.Trial;

/// <summary>
/// The screens that exist for the test alone: the bare page, and a screen with a shortcut, a region that swaps, and a form that refuses an empty name with an error
/// bound to its field. Nothing of production serves them; the host of the test maps them beside the routes of the screens.
/// </summary>
internal static class TrialRoutes
{
    public const string Bare = "/ensaio/base";
    public const string Trial = "/ensaio";
    public const string Catalog = "/ensaio/catalogo";
    public const string Shortcut = "/ensaio/atalho";
    public const string Form = "/ensaio/formulario";
    public const string ShortcutMessage = "O atalho foi acionado.";
    public const string NameRefusal = "Informe o nome.";

    public static void Map(IEndpointRouteBuilder routes)
    {
        _ = routes.MapGet(Bare, (HttpContext context) => Show(context, typeof(BareScreen), "Tela base"));
        _ = routes.MapGet(Catalog, (HttpContext context) => Show(context, typeof(CatalogScreen), "Catálogo dos componentes"));
        _ = routes.MapGet(Trial, (HttpContext context) => Show(context, typeof(TrialScreen), "Tela de ensaio"));
        _ = routes.MapGet(Shortcut, (HttpContext context) => Show(context, typeof(TrialScreen), "Tela de ensaio", Parameters((nameof(TrialScreen.Message), ShortcutMessage))));
        _ = routes.MapPost(Form, AnswerAsync);
    }

    private static async Task<IResult> AnswerAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var name = form["nome"].ToString().Trim();
        return name.Length == 0
            ? Show(context, typeof(TrialScreen), "Tela de ensaio", Parameters((nameof(TrialScreen.NameError), NameRefusal)))
            : Show(context, typeof(TrialScreen), "Tela de ensaio", Parameters((nameof(TrialScreen.Name), name), (nameof(TrialScreen.Saved), $"Nome aceito: {name}.")));
    }

    private static RazorComponentResult<ScreenFrame> Show(HttpContext context, Type screen, string title, IDictionary<string, object?>? parameters = null) =>
        ScreenResult.Of(context, screen, title, parameters);

    private static Dictionary<string, object?> Parameters(params (string Name, object Value)[] given)
    {
        var parameters = new Dictionary<string, object?>();
        foreach (var (name, value) in given)
        {
            parameters[name] = value;
        }

        return parameters;
    }
}
