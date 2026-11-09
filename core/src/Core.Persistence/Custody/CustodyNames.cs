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
        [CustodyItemKind.ContentEpoch] = "content_epoch",
        [CustodyItemKind.Domain] = "domain",
        [CustodyItemKind.SignInAttempt] = "sign_in_attempt",
        [CustodyItemKind.MasterKey] = "master_key",
        [CustodyItemKind.IngestionBatch] = "ingestion_batch",
        [CustodyItemKind.ConfigurationChange] = "configuration_change",
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
        [CustodyStep.ItemEmptied] = "item_emptied",
        [CustodyStep.ItemEmptyingRefused] = "item_emptying_refused",
        [CustodyStep.EpochRotated] = "epoch_rotated",
        [CustodyStep.DomainCreated] = "domain_created",
        [CustodyStep.SealRecorded] = "seal_recorded",
        [CustodyStep.DomainLabelRotated] = "domain_label_rotated",
        [CustodyStep.SignInAccepted] = "sign_in_accepted",
        [CustodyStep.SignInRefused] = "sign_in_refused",
        [CustodyStep.CredentialRecomputed] = "credential_recomputed",
        [CustodyStep.KeyLossDeclared] = "key_loss_declared",
        [CustodyStep.RecoveryTested] = "recovery_tested",
        [CustodyStep.VerifyGuarantees] = "verify_guarantees",
        [CustodyStep.OwnerWindowOpened] = "owner_window_opened",
        [CustodyStep.OwnerWindowClosed] = "owner_window_closed",
        [CustodyStep.Restored] = "restored",
        [CustodyStep.RestoreRehearsed] = "restore_rehearsed",
        [CustodyStep.DomainLabelRevoked] = "domain_label_revoked",
        [CustodyStep.DomainRewrapped] = "domain_rewrapped",
        [CustodyStep.BatchOpened] = "batch_opened",
        [CustodyStep.BatchClosed] = "batch_closed",
        [CustodyStep.PasswordChanged] = "password_changed",
        [CustodyStep.ConfigurationSubmitted] = "configuration_submitted",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<CustodyReason, string> Reasons = new Dictionary<CustodyReason, string>
    {
        [CustodyReason.AdministrativeAct] = "administrative_act",
        [CustodyReason.CorrectionOfRecord] = "correction_of_record",
        [CustodyReason.ContentRead] = "content_read",
        [CustodyReason.VerificationPassed] = "verification_passed",
        [CustodyReason.VerificationFailed] = "verification_failed",
        [CustodyReason.RemovalRequestedBySubject] = "removal_requested_by_subject",
        [CustodyReason.ContractRequirement] = "contract_requirement",
        [CustodyReason.AuthorWithoutFunction] = "author_without_function",
        [CustodyReason.ItemAlreadyEmptied] = "item_already_emptied",
        [CustodyReason.CredentialsAccepted] = "credentials_accepted",
        [CustodyReason.PasswordWrong] = "password_wrong",
        [CustodyReason.AccountNotRecognized] = "account_not_recognized",
        [CustodyReason.BatchAccepted] = "batch_accepted",
        [CustodyReason.BatchRejected] = "batch_rejected",
    }.ToFrozenDictionary();

    internal static string Of(CustodyItemKind kind) => Kinds[kind];

    internal static string Of(CustodyStep step) => Steps[step];

    internal static string Of(CustodyReason reason) => Reasons[reason];
}
