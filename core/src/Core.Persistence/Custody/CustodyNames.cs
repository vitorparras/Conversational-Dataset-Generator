using System.Collections.Frozen;

namespace Core.Persistence.Custody;

/// <summary>The text each item kind, step and reason takes in the chain: fixed here, and never anything a caller writes.</summary>
internal static class CustodyNames
{
    private static readonly FrozenDictionary<CustodyItemKind, string> Kinds = new Dictionary<CustodyItemKind, string>
    {
        [CustodyItemKind.Person] = "person",
        [CustodyItemKind.ItemText] = "item_text",
        [CustodyItemKind.ReversalMap] = "reversal_map",
        [CustodyItemKind.CustodyChain] = "custody_chain",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<CustodyStep, string> Steps = new Dictionary<CustodyStep, string>
    {
        [CustodyStep.PersonCreated] = "person_created",
        [CustodyStep.PersonDeactivated] = "person_deactivated",
        [CustodyStep.PersonFunctionChanged] = "person_function_changed",
        [CustodyStep.PersonPasswordSet] = "person_password_set",
        [CustodyStep.Correction] = "correction",
        [CustodyStep.RawTextRead] = "raw_text_read",
        [CustodyStep.SanitizedTextRead] = "sanitized_text_read",
        [CustodyStep.ReversalEntriesRead] = "reversal_entries_read",
        [CustodyStep.VerifyChain] = "verify_chain",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<CustodyReason, string> Reasons = new Dictionary<CustodyReason, string>
    {
        [CustodyReason.AdministrativeAct] = "administrative_act",
        [CustodyReason.CorrectionOfRecord] = "correction_of_record",
        [CustodyReason.ContentRead] = "content_read",
        [CustodyReason.VerificationPassed] = "verification_passed",
        [CustodyReason.VerificationFailed] = "verification_failed",
    }.ToFrozenDictionary();

    internal static string Of(CustodyItemKind kind) => Kinds[kind];

    internal static string Of(CustodyStep step) => Steps[step];

    internal static string Of(CustodyReason reason) => Reasons[reason];
}
