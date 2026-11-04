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

    internal static IResult Of(PersonResult refused) =>
        Results.Json(
            new OperatorRefused(Fields.GetValueOrDefault(refused.Outcome), refused.Refusal ?? refused.Outcome.ToString()),
            statusCode: Statuses.GetValueOrDefault(refused.Outcome, StatusCodes.Status409Conflict));

    internal static IResult AtField(string name, string reason) => Results.Json(new OperatorRefused(name, reason), statusCode: StatusCodes.Status400BadRequest);

    internal static IResult Forbidden(string reason) => Results.Json(new OperatorRefused(null, reason), statusCode: StatusCodes.Status403Forbidden);
}
