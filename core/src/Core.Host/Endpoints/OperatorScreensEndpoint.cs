using System.Globalization;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Persistence.People;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The screen of the people who operate (#599), for the technical administration alone: the list, filtered and paged by the server with both in the address, read from the same
/// administration the routes of JSON use, and nothing of any material.
/// </summary>
internal static class OperatorScreensEndpoint
{
    private const int OperatorsPlace = 80;

    internal static void Map(IEndpointRouteBuilder endpoints) =>
        _ = endpoints.MapGet(ScreenPaths.Operators, ListAsync).WithMetadata(ScreenAccess.Of(OperatorsScreen.Title, OperatorsPlace, PersonFunctions.Administration));

    /// <summary>With no database declared there is nobody to list, and the page says it is the host that cannot, as the route of JSON does.</summary>
    private static async Task<IResult> ListAsync(HttpContext context, [FromServices] OperatorAdministration? staff)
    {
        if (staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var query = context.Request.Query;
        var (name, function, state) = (query[OperatorsScreen.NameKey].ToString(), query[OperatorsScreen.FunctionKey].ToString(), query[OperatorsScreen.StateKey].ToString());
        var filter = new PersonFilter(Given(name), Given(function), ActiveOf(state));
        var page = int.TryParse(query[OperatorsScreen.PageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var asked) ? asked : 1;
        var screen = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(OperatorsScreen.Page)] = await staff.ListAsync(filter, page, context.RequestAborted),
            [nameof(OperatorsScreen.Name)] = name,
            [nameof(OperatorsScreen.Function)] = function,
            [nameof(OperatorsScreen.State)] = state,
        };
        return ScreenResult.Of(context, typeof(OperatorsScreen), OperatorsScreen.Title, screen);
    }

    private static string? Given(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool? ActiveOf(string state) => state switch
    {
        OperatorsScreen.ActiveState => true,
        OperatorsScreen.InactiveState => false,
        _ => null,
    };
}
