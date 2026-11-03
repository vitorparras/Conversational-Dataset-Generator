using Core.Host.People;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Mvc;

namespace Core.Host.Endpoints;

/// <summary>
/// The routes of a session that make a person, deactivate one and set a password again. Each is done by the person of the session, who is the author of the event, and only if that person
/// holds the administration function, which is judged here until phase 6 brings the authorization. With no database declared the host cannot, and says so.
/// </summary>
internal static class PeopleEndpoint
{
    internal const string Route = "/people";

    private const string NotAdministrator = "Só quem tem a função Administração técnica cria, desativa ou repõe a senha de uma pessoa; nada foi gravado.";
    private const string BlankSecret = "A senha não pode ser vazia.";

    internal static void Map(IEndpointRouteBuilder endpoints)
    {
        _ = endpoints.MapPost(Route, MakeAsync);
        _ = endpoints.MapPost(Route + "/{id}/deactivate", DeactivateAsync);
        _ = endpoints.MapPut(Route + "/{id}/password", SetPasswordAsync);
    }

    private static Task<IResult> MakeAsync([FromBody] PersonAsked asked, [FromServices] PeopleAdministration? people, HttpContext context) =>
        ActAsync(people, context, async (administration, author) =>
        {
            if (asked.Password.Length == 0)
            {
                return PersonAnswer.AtField("senha", BlankSecret);
            }

            var (made, password) = await administration.CreateAsync(author, new NewPerson(asked.Name, asked.Function), asked.Password, context.RequestAborted);
            return (made, password) switch
            {
                ({ Outcome: not PersonOutcome.Done }, _) => PersonAnswer.Of(made),
                (_, { Outcome: PersonOutcome.Done }) => Results.Created(new Uri($"{Route}/{made.PersonId}", UriKind.Relative), new PersonMade(made.PersonId!.Value)),
                _ => Results.Json(new PersonRefused(null, Unfinished(made.PersonId!.Value)), statusCode: StatusCodes.Status500InternalServerError),
            };
        });

    private static Task<IResult> DeactivateAsync(long id, [FromServices] PeopleAdministration? people, HttpContext context) =>
        ActAsync(people, context, async (administration, author) =>
            Of(await administration.DeactivateAsync(author, id, context.RequestAborted), id));

    private static Task<IResult> SetPasswordAsync(long id, [FromBody] PasswordAsked asked, [FromServices] PeopleAdministration? people, HttpContext context) =>
        ActAsync(people, context, async (administration, author) =>
            Of(await administration.SetPasswordAsync(author, id, asked.Password, context.RequestAborted), id));

    /// <summary>Runs the act as the person of the session when they hold the administration function; otherwise refuses with 403 and the act never starts.</summary>
    private static async Task<IResult> ActAsync(PeopleAdministration? people, HttpContext context, Func<PeopleAdministration, long, Task<IResult>> act)
    {
        if (people is null)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        var sessions = context.RequestServices.GetRequiredService<SessionStore>();
        return sessions.Resolve(SessionCookie.Read(context.Request)) is { } author && await people.IsAdministratorAsync(author, context.RequestAborted)
            ? await act(people, author)
            : PersonAnswer.Forbidden(NotAdministrator);
    }

    private static IResult Of(PersonResult result, long id) =>
        result.Outcome == PersonOutcome.Done ? Results.Ok(new PersonMade(id)) : PersonAnswer.Of(result);

    private static string Unfinished(long id) =>
        $"A pessoa foi criada com o id {id} e a senha não foi definida; defina-a pela rota PUT {Route}/{id}/password.";
}
