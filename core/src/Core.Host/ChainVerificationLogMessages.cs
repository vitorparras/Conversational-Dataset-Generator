using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.ServerGuarantees;

namespace Core.Host;

/// <summary>What the host writes about the verification of the chain: counts, sequences and kinds, never a field of any event, nor the text of a failure.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class ChainVerificationLogMessages
{
    [LoggerMessage(EventId = 320, Level = LogLevel.Information, Message = "Verificação da cadeia de custódia: íntegra, {EventsRead} eventos lidos; registro do veredito: {Recorded}")]
    internal static partial void ChainHeld(this ILogger logger, long eventsRead, CustodyOutcome recorded);

    [LoggerMessage(
        EventId = 321,
        Level = LogLevel.Error,
        Message = "Verificação da cadeia de custódia: QUEBRADA no evento de sequência {Where}, {EventsRead} eventos lidos; registro do veredito: {Recorded}")]
    internal static partial void ChainBroken(this ILogger logger, string where, long eventsRead, CustodyOutcome recorded);

    [LoggerMessage(EventId = 322, Level = LogLevel.Error, Message = "Verificação da cadeia de custódia não chegou ao fim: {FailureType}; a próxima sai no intervalo seguinte")]
    internal static partial void VerificationFailed(this ILogger logger, string failureType);

    [LoggerMessage(EventId = 324, Level = LogLevel.Information, Message = "Conferência das garantias do servidor: todas valem; registro do veredito: {Recorded}")]
    internal static partial void GuaranteesHeld(this ILogger logger, CustodyOutcome recorded);

    [LoggerMessage(EventId = 325, Level = LogLevel.Error, Message = "Conferência das garantias do servidor: FALTAM {Missing}; registro do veredito: {Recorded}")]
    internal static partial void GuaranteesMissing(this ILogger logger, GuaranteeKinds missing, CustodyOutcome recorded);

    [LoggerMessage(EventId = 326, Level = LogLevel.Error, Message = "Conferência das garantias do servidor não chegou ao fim: {FailureType}; a próxima sai no intervalo seguinte")]
    internal static partial void GuaranteesNotConferred(this ILogger logger, string failureType);

    [LoggerMessage(EventId = 323, Level = LogLevel.Error, Message = "Leitura da última verificação da cadeia falhou: {FailureType}; a resposta foi 503")]
    internal static partial void LastUnreadable(this ILogger logger, string failureType);
}
