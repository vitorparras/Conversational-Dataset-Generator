using System.Collections.Frozen;

namespace Core.Persistence.Custody;

/// <summary>The text each item kind, step and reason takes in the chain: fixed here, and never anything a caller writes.</summary>
internal static class CustodyNames
{
    private static readonly FrozenDictionary<CustodyItemKind, string> Kinds = new Dictionary<CustodyItemKind, string>
    {
        [CustodyItemKind.Person] = "person",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<CustodyStep, string> Steps = new Dictionary<CustodyStep, string>
    {
        [CustodyStep.PersonCreated] = "person_created",
        [CustodyStep.PersonDeactivated] = "person_deactivated",
        [CustodyStep.PersonFunctionChanged] = "person_function_changed",
        [CustodyStep.PersonPasswordSet] = "person_password_set",
        [CustodyStep.Correction] = "correction",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<CustodyReason, string> Reasons = new Dictionary<CustodyReason, string>
    {
        [CustodyReason.AdministrativeAct] = "administrative_act",
        [CustodyReason.CorrectionOfRecord] = "correction_of_record",
    }.ToFrozenDictionary();

    internal static string Of(CustodyItemKind kind) => Kinds[kind];

    internal static string Of(CustodyStep step) => Steps[step];

    internal static string Of(CustodyReason reason) => Reasons[reason];
}
