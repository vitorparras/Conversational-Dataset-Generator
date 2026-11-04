using System.Diagnostics.CodeAnalysis;
using Core.Boundary.Http;
using Core.Persistence.Custody;

namespace Core.Host;

/// <summary>What the host writes about sending the seal: the count of the seal, the sequence of its record and the reason of a failure, never the seal itself.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SealSendingLogMessages
{
    [LoggerMessage(EventId = 326, Level = LogLevel.Information, Message = "Selo da cadeia de custódia enviado e registrado: {SealedEvents} eventos, registro de sequência {Sequence}")]
    internal static partial void SealSentAndRecorded(this ILogger logger, long sealedEvents, long? sequence);

    [LoggerMessage(
        EventId = 327,
        Level = LogLevel.Error,
        Message = "Selo da cadeia de custódia NÃO enviado, nada foi registrado: {Failure} (resposta {Status}); o selo contava {SealedEvents} eventos; a próxima tentativa sai no intervalo seguinte")]
    internal static partial void SealNotDelivered(this ILogger logger, SealDeliveryFailure failure, int status, long sealedEvents);

    [LoggerMessage(
        EventId = 328,
        Level = LogLevel.Error,
        Message = "Selo da cadeia de custódia enviado com {SealedEvents} eventos, mas o registro foi recusado ({Outcome}); a próxima tentativa sai no intervalo seguinte")]
    internal static partial void SealSentButNotRecorded(this ILogger logger, long sealedEvents, CustodyOutcome outcome);

    [LoggerMessage(
        EventId = 329,
        Level = LogLevel.Error,
        Message = "O envio do selo da cadeia de custódia não chegou ao fim: {FailureType}; nada foi registrado e a próxima tentativa sai no intervalo seguinte")]
    internal static partial void SealSendingFailed(this ILogger logger, string failureType);
}
