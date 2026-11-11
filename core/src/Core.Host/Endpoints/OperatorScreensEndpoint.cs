using System.Globalization;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screens of the people who operate (#599), for the technical administration alone: the list, filtered and paged by the server with both in the address, and the form that
/// registers one, which calls what the route of JSON calls. A refusal is the same form again with the error bound to its field and what was typed, never the password;
/// a registration takes the browser to the list filtered by the name just registered.
/// </summary>
internal static class OperatorScreensEndpoint
{
    private const int OperatorsPlace = 80;

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapGet(ScreenPaths.Operators, ListAsync).WithMetadata(ScreenAccess.Of(OperatorsScreen.Title, OperatorsPlace, PersonFunctions.Administration));
        _ = endpoints.MapGet(ScreenPaths.OperatorNew, Form).WithMetadata(ScreenAccess.Unlisted(PersonFunctions.Administration));
        _ = endpoints.MapPost(ScreenPaths.OperatorNew, OperatorRegistration.RegisterAsync).WithMetadata(ScreenAccess.Unlisted(PersonFunctions.Administration));
        OperatorEmailEndpoint.Map(endpoints);
    }

    /// <summary>With no database declared there is nobody to list, and the page says it is the host that cannot, as the route of JSON does.</summary>
    private static async Task<IResult> ListAsync(HttpContext context, [FromServices] OperatorAdministration? staff)
    {
        if (staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var query = context.Request.Query;
        var filter = FilterOf(query);
        var page = int.TryParse(query[OperatorsScreen.PageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) ? asked : 1;
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(OperatorsScreen.Page)] = await staff.ListAsync(filter, page, context.RequestAborted),
            [nameof(OperatorsScreen.Name)] = query[OperatorsScreen.NameKey].ToString(),
            [nameof(OperatorsScreen.Function)] = filter.Function,
            [nameof(OperatorsScreen.State)] = query[OperatorsScreen.StateKey].ToString(),
            [nameof(OperatorsScreen.Registered)] = RegisteredNotice.Take(context) is { } registered ? await staff.Registry.NameOfAsync(registered, context.RequestAborted) : null,
        };
        return ScreenResult.Of(context, typeof(OperatorsScreen), OperatorsScreen.Title, screen);
    }

    private static RazorComponentResult<ScreenFrame> Form(HttpContext context) => ScreenResult.Of(context, typeof(OperatorFormScreen), OperatorFormScreen.Title);

    /// <summary>The filter the address asks for; a function the registry does not accept is left out, as the list of choices shows, instead of leaving nobody.</summary>
    internal static PersonFilter FilterOf(IQueryCollection query)
    {
        var function = query[OperatorsScreen.FunctionKey].ToString();
        return new PersonFilter(Given(query[OperatorsScreen.NameKey].ToString()), PersonFunctions.IsAccepted(function) ? function : null, ActiveOf(query[OperatorsScreen.StateKey].ToString()));
    }

    private static string? Given(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool? ActiveOf(string state) => state switch
    {
        OperatorsScreen.ActiveState => true,
        OperatorsScreen.InactiveState => false,
        _ => null,
    };
}
