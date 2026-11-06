using System.Collections.Concurrent;
using Core.Host.Screens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Core.Host.Tests.Browser.Trial;

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
    public const string Submit = "/ensaio/envio";
    public const string List = "/ensaio/lista";
    public const string Choice = "/ensaio/caixa";
    public const string Failure = "/ensaio/falha";
    public const string InternalDetail = "zqx-detalhe-interno-6710";
    public const string ShortcutMessage = "O atalho foi acionado.";
    public const string NameRefusal = "Informe o nome.";

    private static readonly ConcurrentDictionary<string, int> Posts = new();

    public static void Map(IEndpointRouteBuilder routes)
    {
        _ = routes.MapGet(Bare, (HttpContext context) => Show(context, typeof(BareScreen), "Tela base"));
        _ = routes.MapGet(Catalog, (HttpContext context) => Show(context, typeof(CatalogScreen), "Catálogo dos componentes"));
        _ = routes.MapGet(Trial, (HttpContext context) => Show(context, typeof(TrialScreen), "Tela de ensaio"));
        _ = routes.MapGet(Shortcut, (HttpContext context) => Show(context, typeof(TrialScreen), "Tela de ensaio", Parameters((nameof(TrialScreen.Message), ShortcutMessage))));
        _ = routes.MapGet(List, ShowList);
        _ = routes.MapGet(Choice, (HttpContext context) => Show(context, typeof(ChoiceScreen), "Tela de escolha"));
        _ = routes.MapGet(Failure, Fail);
        _ = routes.MapPost(Form, AnswerAsync);
        _ = routes.MapGet(Submit + "/{mode}", ShowSubmit);
        _ = routes.MapPost(Submit + "/{mode}", ReceiveAsync);
    }

    /// <summary>The form of the mode, carrying the identifier that the test chose for this run, so that no other test's posts are counted with its own.</summary>
    private static RazorComponentResult<ScreenFrame> ShowSubmit(string mode, string? key, HttpContext context) =>
        Show(context, typeof(SubmitScreen), "Envio sem região", Parameters((nameof(SubmitScreen.Action), $"{Submit}/{mode}"), (nameof(SubmitScreen.Key), key ?? "")));

    /// <summary>How many posts with the identifier reached the server.</summary>
    public static int Received(string key) => Posts.GetValueOrDefault(key);

    /// <summary>Counts the post, then answers without the region the client asked for: another screen, a failure of the server, or a redirect to another screen.</summary>
    private static async Task<IResult> ReceiveAsync(string mode, HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        _ = Posts.AddOrUpdate(form["chave"].ToString(), 1, (_, count) => count + 1);
        return mode switch
        {
            "erro" => Results.Text("Falha do servidor no ensaio.", statusCode: StatusCodes.Status500InternalServerError),
            "redireciona" => Results.Redirect(Bare),
            "falha" => Fail(),
            _ => Show(context, typeof(BareScreen), "Tela base"),
        };
    }

    private static async Task<IResult> AnswerAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var form = await context.Request.ReadFormAsync(cancellationToken);
        var name = form["nome"].ToString().Trim();
        return name.Length == 0
            ? Show(context, typeof(TrialScreen), "Tela de ensaio", Parameters((nameof(TrialScreen.NameError), NameRefusal)))
            : Show(context, typeof(TrialScreen), "Tela de ensaio", Parameters((nameof(TrialScreen.Name), name), (nameof(TrialScreen.Saved), $"Nome aceito: {name}.")));
    }

    private static RazorComponentResult<ScreenFrame> ShowList(int? pagina, string? filtro, HttpContext context) =>
        Show(context, typeof(ListScreen), "Lista de ensaio", Parameters((nameof(ListScreen.Page), Math.Max(pagina ?? 1, 1)), (nameof(ListScreen.Filter), filtro ?? "")));

    /// <summary>The failure nobody handles: a screen that throws, with a message that carries what the person must never see.</summary>
    private static IResult Fail() => throw new InvalidOperationException(InternalDetail);

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
