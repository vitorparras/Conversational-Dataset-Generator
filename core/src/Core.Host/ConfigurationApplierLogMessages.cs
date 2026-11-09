using System.Diagnostics.CodeAnalysis;
using Core.Host.Settings;

namespace Core.Host;

/// <summary>What the applier of the configuration writes: the submission, the reason and the sequences of the two events of the chain, never the value of a key, a name or an address of a person.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class ConfigurationApplierLogMessages
{
    [LoggerMessage(
        EventId = 343,
        Level = LogLevel.Information,
        Message = "Alteração de configuração {Change} aplicada ao arquivo: submissão de sequência {SubmissionSequence}, aprovação de sequência {ApprovalSequence}")]
    internal static partial void ChangeApplied(this ILogger logger, long change, long submissionSequence, long approvalSequence);

    [LoggerMessage(
        EventId = 344,
        Level = LogLevel.Error,
        Message = "Alteração de configuração {Change} NÃO aplicada, o arquivo ficou como estava: {Reason}; submissão de sequência {SubmissionSequence}, aprovação de sequência {ApprovalSequence}")]
    internal static partial void ChangeNotApplied(this ILogger logger, long change, ApplicationRefusal reason, long submissionSequence, long approvalSequence);
}
