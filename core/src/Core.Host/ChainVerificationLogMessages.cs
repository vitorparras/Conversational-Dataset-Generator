using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Verification;

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

    [LoggerMessage(EventId = 323, Level = LogLevel.Error, Message = "Leitura da última verificação da cadeia falhou: {FailureType}; a resposta foi 503")]
    internal static partial void LastUnreadable(this ILogger logger, string failureType);

    [LoggerMessage(
        EventId = 324,
        Level = LogLevel.Error,
        Message = "Verificação da cadeia de custódia contra o selo: NÃO CONFERE ({Outcome}); o selo contava {SealedEvents} eventos e a cadeia tem {EventsRead}")]
    internal static partial void SealDoesNotHold(this ILogger logger, SealOutcome outcome, long sealedEvents, long eventsRead);

    [LoggerMessage(EventId = 325, Level = LogLevel.Error, Message = "Verificação da cadeia de custódia contra o selo não chegou ao fim: {FailureType}; a resposta foi 503")]
    internal static partial void SealVerificationFailed(this ILogger logger, string failureType);

    [LoggerMessage(EventId = 330, Level = LogLevel.Error, Message = "O selo manual não pôde ser emitido nem registrado: {FailureType}; a resposta foi 503")]
    internal static partial void SealRouteUnavailable(this ILogger logger, string failureType);

    [LoggerMessage(EventId = 331, Level = LogLevel.Error, Message = "A renovação do aviso do selo não chegou ao fim: {FailureType}; o aviso fica como estava e a próxima renovação sai no intervalo seguinte")]
    internal static partial void SealWarningNotRenewed(this ILogger logger, string failureType);

    [LoggerMessage(EventId = 332, Level = LogLevel.Error, Message = "A leitura da data do último selo falhou: {FailureType}; a resposta foi 503")]
    internal static partial void SealDateUnreadable(this ILogger logger, string failureType);
}
