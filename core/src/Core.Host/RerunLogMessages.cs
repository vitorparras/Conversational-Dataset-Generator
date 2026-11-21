using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content.Rerun;

namespace Core.Host;

/// <summary>What the host writes about the rerun of the queue: the domain, the item, the outcome and the type of a failure, never a word of the material.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class RerunLogMessages
{
    [LoggerMessage(EventId = 352, Level = LogLevel.Information, Message = "Reexecução da sanitização: item {ItemId} do domínio {DomainId}, {Outcome}")]
    internal static partial void RerunConcluded(this ILogger logger, long itemId, int domainId, RerunOutcome outcome);

    [LoggerMessage(EventId = 353, Level = LogLevel.Warning, Message = "Reexecução da sanitização: item {ItemId} do domínio {DomainId} recusado, motivo {Refusal}")]
    internal static partial void RerunRefused(this ILogger logger, long itemId, int domainId, RerunRefusal refusal);

    [LoggerMessage(
        EventId = 354,
        Level = LogLevel.Warning,
        Message = "Reexecução da sanitização: item {ItemId} do domínio {DomainId} falhou ({FailureType}) e gastou uma tentativa; na última, é recusado")]
    internal static partial void RerunFailed(this ILogger logger, long itemId, int domainId, string failureType);

    [LoggerMessage(
        EventId = 355,
        Level = LogLevel.Error,
        Message = "Reexecução da sanitização: o item {ItemId} do domínio {DomainId} voltou à fila, a chave mestra não foi lida; nova tentativa no ciclo seguinte")]
    internal static partial void RerunKeyUnread(this ILogger logger, long itemId, int domainId);

    [LoggerMessage(
        EventId = 356,
        Level = LogLevel.Error,
        Message = "Reexecução da sanitização interrompida: {FailureType}; o item da vez volta à fila quando a posse vencer, e o ciclo seguinte tenta de novo")]
    internal static partial void RerunInterrupted(this ILogger logger, string failureType);
}
