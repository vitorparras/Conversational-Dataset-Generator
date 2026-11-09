using Core.Host.Operators;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The routes of a session that make a person, deactivate one and set a password again. Each is done by the person of the session, who is the author of the event, and only if that person
/// holds the administration function, which is judged here until phase 6 brings the authorization. With no database declared the host cannot, and says so.
/// </summary>
internal static class OperatorsEndpoint
{
    internal const string Route = "/people";

    private const string NotAdministrator = "Só quem tem a função Administração técnica cria, desativa ou repõe a senha de uma pessoa; nada foi gravado.";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(Route, MakeAsync);
        _ = endpoints.MapPost(Route + "/{id}/deactivate", DeactivateAsync);
        _ = endpoints.MapPut(Route + "/{id}/password", SetPasswordAsync);
    }

    private static Task<IResult> MakeAsync([FromBody] OperatorAsked asked, [FromServices] OperatorAdministration? staff, HttpContext context) =>
        ActAsync(staff, context, async (administration, author) =>
        {
            var (made, password) = await administration.RegisterAsync(author, new NewPerson(asked.Name, asked.Function), asked.Password, context.RequestAborted);
            var registered = OperatorAnswer.Registered(made, password);
            return registered.Id is { } id
                ? Results.Created(new Uri($"{Route}/{id}", UriKind.Relative), new OperatorMade(id))
                : Results.Json(registered.Refusal, statusCode: registered.Status);
        });

    private static Task<IResult> DeactivateAsync(long id, [FromServices] OperatorAdministration? staff, HttpContext context) =>
        ActAsync(staff, context, async (administration, author) =>
            Of(await administration.DeactivateAsync(author, id, context.RequestAborted), id));

    private static Task<IResult> SetPasswordAsync(long id, [FromBody] PasswordAsked asked, [FromServices] OperatorAdministration? staff, HttpContext context) =>
        ActAsync(staff, context, async (administration, author) =>
            Of(await administration.SetPasswordAsync(author, id, asked.Password, context.RequestAborted), id));

    /// <summary>Runs the act as the person of the session when they hold the administration function; otherwise refuses with 403 and the act never starts.</summary>
    private static async Task<IResult> ActAsync(OperatorAdministration? staff, HttpContext context, Func<OperatorAdministration, long, Task<IResult>> act)
    {
        if (staff is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var sessions = context.RequestServices.GetRequiredService<SessionStore>();
        return sessions.Resolve(SessionCookie.Read(context.Request)) is { } author && await staff.IsAdministratorAsync(author, context.RequestAborted)
            ? await act(staff, author)
            : OperatorAnswer.Forbidden(NotAdministrator);
    }

    private static IResult Of(PersonResult result, long id) =>
        result.Outcome == PersonOutcome.Done ? Results.Ok(new OperatorMade(id)) : OperatorAnswer.Of(result);
}
