using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;

namespace Core.Host;

/// <summary>What the host writes when it marks an approved change as in force: the submission, the sequences and the reason a record was not made, never a value or a person.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class ConfigurationInForceLogMessages
{
    [LoggerMessage(
        EventId = 345,
        Level = LogLevel.Information,
        Message = "Alteração de configuração {Change} em vigor: aprovação de sequência {ApprovalSequence}, registro de sequência {Sequence}")]
    internal static partial void ChangeInForce(this ILogger logger, long change, long approvalSequence, long? sequence);

    [LoggerMessage(
        EventId = 346,
        Level = LogLevel.Error,
        Message = "Alteração de configuração {Change} em vigor, mas o registro foi recusado ({Outcome}); a próxima partida tenta de novo")]
    internal static partial void ChangeInForceNotRecorded(this ILogger logger, long change, CustodyOutcome outcome);

    [LoggerMessage(
        EventId = 347,
        Level = LogLevel.Error,
        Message = "O registro das alterações de configuração em vigor não chegou ao fim: {FailureType}; a próxima partida tenta de novo")]
    internal static partial void ConfigurationInForceFailed(this ILogger logger, string failureType);
}
