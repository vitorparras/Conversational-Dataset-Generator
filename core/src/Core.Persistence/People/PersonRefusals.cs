using System.Globalization;
using Core.Persistence.Custody;

namespace Core.Persistence.People;

/// <summary>The reasons the registry gives for a refusal, in the words of whoever operates.</summary>
internal static class PersonRefusals
{
    internal static PersonResult? Judge(NewPerson person) =>
        InvalidName(person.Name) ?? NotAccepted(person.Function);

    internal static PersonResult? NotAccepted(string function) =>
        PersonFunctions.IsAccepted(function)
            ? null
            : PersonResult.Refused(
                PersonOutcome.FunctionNotAccepted,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"A função '{function}' não consta da política de acesso. As aceitas são: {string.Join(", ", PersonFunctions.Accepted)}."));

    internal static PersonResult NameTaken() =>
        PersonResult.Refused(PersonOutcome.NameTaken, "Já existe uma pessoa com esse nome de uso.");

    internal static PersonResult ActorUnknown() =>
        PersonResult.Refused(PersonOutcome.ActorUnknown, "A pessoa que age não está cadastrada.");

    internal static PersonResult ActorDeactivated() =>
        PersonResult.Refused(PersonOutcome.ActorDeactivated, "A pessoa que age está desativada.");

    internal static PersonResult PasswordInvalid() =>
        PersonResult.Refused(PersonOutcome.PasswordInvalid, "A senha não pode ser vazia.");

    /// <summary>The answer for a change whose custody event was attempted: done when the event is in the chain, otherwise the reason it was not.</summary>
    internal static PersonResult Chained(CustodyResult custody, long personId) => custody.Outcome switch
    {
        CustodyOutcome.Appended => PersonResult.Done(personId),
        CustodyOutcome.ActorUnknown => ActorUnknown(),
        CustodyOutcome.ActorDeactivated => ActorDeactivated(),
        CustodyOutcome.CorrectedNotFound or CustodyOutcome.Conflict => ChainBusy(),
        _ => throw new ArgumentOutOfRangeException(nameof(custody), custody.Outcome, "The outcome is not one a change of a person can meet."),
    };

    internal static PersonResult ChainBusy() =>
        PersonResult.Refused(PersonOutcome.ChainBusy, "Outra gravação chegou ao mesmo elo da cadeia de custódia antes; nada foi gravado, e o pedido pode ser repetido.");

    internal static PersonResult NotFound() =>
        PersonResult.Refused(PersonOutcome.NotFound, "Não há pessoa com esse identificador.");

    internal static PersonResult Deactivated() =>
        PersonResult.Refused(PersonOutcome.Deactivated, "A pessoa está desativada.");

    internal static PersonResult FunctionUnchanged() =>
        PersonResult.Refused(PersonOutcome.FunctionUnchanged, "A pessoa já tem essa função.");

    private static PersonResult? InvalidName(string name) =>
        string.IsNullOrEmpty(name) || name != name.Trim()
            ? PersonResult.Refused(PersonOutcome.NameInvalid, "O nome de uso não pode ser vazio nem ter espaço no começo ou no fim.")
            : null;
}
