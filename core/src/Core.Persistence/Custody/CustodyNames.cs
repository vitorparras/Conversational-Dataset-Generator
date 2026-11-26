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
        [CustodyItemKind.SanitizationRerun] = "sanitization_rerun",
        [CustodyItemKind.ReversalRequest] = "reversal_request",
        [CustodyItemKind.ReversalAuthorization] = "reversal_authorization",
        [CustodyItemKind.ReversalRevelation] = "reversal_revelation",
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
        [CustodyStep.OriginRead] = "origin_read",
        [CustodyStep.OccurrencesRead] = "occurrences_read",
        [CustodyStep.BatchSampled] = "batch_sampled",
        [CustodyStep.BatchReviewed] = "batch_reviewed",
        [CustodyStep.BatchSanitized] = "batch_sanitized",
        [CustodyStep.ConfigurationApproved] = "configuration_approved",
        [CustodyStep.ConfigurationRefused] = "configuration_refused",
        [CustodyStep.ConfigurationInForce] = "configuration_in_force",
        [CustodyStep.ConfigurationNotApplied] = "configuration_not_applied",
        [CustodyStep.PersonEmailCorrected] = "person_email_corrected",
        [CustodyStep.SanitizationRerun] = "sanitization_rerun",
        [CustodyStep.ItemResanitized] = "item_resanitized",
        [CustodyStep.BatchPseudonymized] = "batch_pseudonymized",
        [CustodyStep.ReversalRequested] = "reversal_requested",
        [CustodyStep.ReversalRequestRefused] = "reversal_request_refused",
        [CustodyStep.ReversalRequestRead] = "reversal_request_read",
        [CustodyStep.ReversalAuthorized] = "reversal_authorized",
        [CustodyStep.ReversalAuthorizationRefused] = "reversal_authorization_refused",
        [CustodyStep.ReversalDeclined] = "reversal_declined",
        [CustodyStep.ReversalRevealed] = "reversal_revealed",
        [CustodyStep.ReversalDenied] = "reversal_denied",
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
        [CustodyReason.ReviewClean] = "review_clean",
        [CustodyReason.ReviewFound] = "review_found",
        [CustodyReason.SanitizationIdentical] = "sanitization_identical",
        [CustodyReason.SanitizationReplaced] = "sanitization_replaced",
        [CustodyReason.SanitizationRefused] = "sanitization_refused",
        [CustodyReason.SanitizationSuperseded] = "sanitization_superseded",
        [CustodyReason.ReversalFunction] = "reversal_function",
        [CustodyReason.ReversalScope] = "reversal_scope",
        [CustodyReason.ReversalSelf] = "reversal_self",
        [CustodyReason.ReversalExpired] = "reversal_expired",
        [CustodyReason.ReversalUsed] = "reversal_used",
        [CustodyReason.ReversalEmptied] = "reversal_emptied",
    }.ToFrozenDictionary();

    internal static string Of(CustodyItemKind kind) => Kinds[kind];

    internal static string Of(CustodyStep step) => Steps[step];

    internal static string Of(CustodyReason reason) => Reasons[reason];
}
