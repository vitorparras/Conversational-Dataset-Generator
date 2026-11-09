using System.Diagnostics.CodeAnalysis;
using Core.Host.Settings;
using Core.Persistence.Custody;

namespace Core.Host;

/// <summary>What the host writes when it records that an approved change will never be applied: the submission, the reason and the sequences, never a value or a person.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class ConfigurationRefusalsLogMessages
{
    [LoggerMessage(
        EventId = 348,
        Level = LogLevel.Warning,
        Message = "Alteração de configuração {Change} não será aplicada: {Reason}; aprovação de sequência {ApprovalSequence}, registro de sequência {Sequence}")]
    internal static partial void ChangeWillNotBeApplied(this ILogger logger, long change, ApplicationRefusal reason, long approvalSequence, long? sequence);

    [LoggerMessage(
        EventId = 349,
        Level = LogLevel.Error,
        Message = "Alteração de configuração {Change} não será aplicada ({Reason}), mas o registro foi recusado ({Outcome}); a próxima rodada tenta de novo")]
    internal static partial void ChangeWillNotBeAppliedNotRecorded(this ILogger logger, long change, ApplicationRefusal reason, CustodyOutcome outcome);

    [LoggerMessage(
        EventId = 350,
        Level = LogLevel.Error,
        Message = "O registro das alterações de configuração que não serão aplicadas não chegou ao fim: {FailureType}; a próxima rodada tenta de novo")]
    internal static partial void ConfigurationRefusalsFailed(this ILogger logger, string failureType);
}
