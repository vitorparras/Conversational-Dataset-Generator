using Core.Host.Endpoints;
using Core.Host.Screens;
using Core.Host.Tests.Browser.Trial;
using Core.Persistence.People;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the entry, the start page and the access policy of the screens (issue 594) behind the real session gate and the real screen gate, with the people the host makes up for the
/// run, each with the function that opens a different part of the menu: the administration, which reaches the screen of the administration and the audit; the audit, which reaches the
/// audit only; and an annotation, which reaches nothing while no domain is granted. The screens of the test are <see cref="OfAdministration"/>, <see cref="OfAudit"/> and
/// <see cref="OfAnnotation"/>, mapped in the opposite order of their places in the menu, and a route that does not exist, <see cref="Missing"/>.
/// </summary>
internal static class NavigationCase
{
    public const string OfAdministration = "/zqx-administracao-5940";
    public const string OfAudit = "/zqx-auditoria-5940";
    public const string OfAnnotation = "/zqx-anotacao-5940";
    public const string Missing = "/zqx-nao-existe-5940";
    public const string AdministrationCaption = "Administração";
    public const string AuditCaption = "Auditoria";

    public static ScreenParts Parts { get; } = new() { Screens = MapScreens };

    /// <summary>The screens of the test, with their entries at places that are not the order they are mapped in, and one that only a person with a domain reaches.</summary>
    private static void MapScreens(IEndpointRouteBuilder routes)
    {
        _ = routes.MapGet(OfAnnotation, Show).WithMetadata(ScreenAccess.Of("Anotação", 30, "Anotação"));
        _ = routes.MapGet(OfAudit, Show).WithMetadata(ScreenAccess.Of(AuditCaption, 20, PersonFunctions.Audit, PersonFunctions.Administration));
        _ = routes.MapGet(OfAdministration, Show).WithMetadata(ScreenAccess.Of(AdministrationCaption, 10, PersonFunctions.Administration));
        NoticeScreensEndpoint.Map(routes);
    }

    private static RazorComponentResult<ScreenFrame> Show(HttpContext context) => ScreenResult.Of(context, typeof(BareScreen), "Tela de ensaio");
}
