using Core.Persistence.People;

namespace Core.Host.Endpoints;

/// <summary>How a refusal of the registry is answered: the status that fits it, the field at fault when one is, and the reason the registry gave in words.</summary>
internal static class OperatorAnswer
{
    private static readonly Dictionary<PersonOutcome, string> Fields = new()
    {
        { PersonOutcome.NameInvalid, "nome" },
        { PersonOutcome.NameTaken, "nome" },
        { PersonOutcome.FunctionNotAccepted, "função" },
        { PersonOutcome.PasswordInvalid, "senha" },
    };

    private static readonly Dictionary<PersonOutcome, int> Statuses = new()
    {
        { PersonOutcome.NameInvalid, StatusCodes.Status400BadRequest },
        { PersonOutcome.FunctionNotAccepted, StatusCodes.Status400BadRequest },
        { PersonOutcome.PasswordInvalid, StatusCodes.Status400BadRequest },
        { PersonOutcome.ActorUnknown, StatusCodes.Status403Forbidden },
        { PersonOutcome.ActorDeactivated, StatusCodes.Status403Forbidden },
        { PersonOutcome.NotFound, StatusCodes.Status404NotFound },
        { PersonOutcome.ChainBusy, StatusCodes.Status503ServiceUnavailable },
        { PersonOutcome.ChainBroken, StatusCodes.Status503ServiceUnavailable },
    };

    /// <summary>The field of the form a refusal is about, as the route names it: the name, the function or the password; null when the refusal is about no field.</summary>
    /// <param name="outcome">How the operation ended.</param>
    internal static string? FieldOf(PersonOutcome outcome) => Fields.GetValueOrDefault(outcome);

    /// <summary>The refusal and the status a refused operation is answered with, which the screen reads as the route does.</summary>
    /// <param name="refused">The refused result.</param>
    internal static (OperatorRefused Refusal, int Status) RefusalOf(PersonResult refused) =>
        (new OperatorRefused(FieldOf(refused.Outcome), refused.Refusal ?? refused.Outcome.ToString()), Statuses.GetValueOrDefault(refused.Outcome, StatusCodes.Status409Conflict));

    internal static IResult Of(PersonResult refused)
    {
        var (refusal, status) = RefusalOf(refused);
        return Results.Json(refusal, statusCode: status);
    }

    internal static IResult AtField(string name, string reason) => Results.Json(new OperatorRefused(name, reason), statusCode: StatusCodes.Status400BadRequest);

    internal static IResult Forbidden(string reason) => Results.Json(new OperatorRefused(null, reason), statusCode: StatusCodes.Status403Forbidden);
}
