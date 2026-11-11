using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Ingestion;

namespace Core.Host.Submissions;

/// <summary>What the host writes about a submission that could not be tidied: the batch, the domain and how the closing ended, never a byte of the file, a name or the origin.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SubmissionLogMessages
{
    [LoggerMessage(
        EventId = 350,
        Level = LogLevel.Error,
        Message = "Lote {Batch} do domínio {Domain} ficou aberto com os itens gravados: a rejeição terminou em {Outcome}; quem administra o servidor decide o que fazer com ele")]
    internal static partial void BatchLeftOpen(this ILogger logger, long batch, int domain, BatchOutcome outcome);
}
