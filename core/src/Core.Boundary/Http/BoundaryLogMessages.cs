using System.Diagnostics.CodeAnalysis;
using Core.Boundary.Contracts;
using Microsoft.Extensions.Logging;

namespace Core.Boundary.Http;

/// <summary>What the recognizer writes about a request: its opaque reference, the outcome and the time; never the text, an offset, a body or the secret.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class BoundaryLogMessages
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "Pedido analisado pelo serviço de linguagem")]
    internal static partial void Analyzed(this ILogger logger, [LogProperties(OmitReferenceName = true)] AnalysisSummary analysis);

    [LoggerMessage(EventId = 201, Level = LogLevel.Error, Message = "Pedido {Reference} sem análise: {Reason} em {ElapsedMs} ms")]
    internal static partial void Unavailable(this ILogger logger, string reference, UnavailabilityReason reason, long elapsedMs);

    [LoggerMessage(EventId = 202, Level = LogLevel.Error, Message = "Pedido {Reference} sem análise: resposta recusada por {Problem} em {ElapsedMs} ms")]
    internal static partial void AnswerRefused(this ILogger logger, string reference, ValidationProblem problem, long elapsedMs);

    [LoggerMessage(EventId = 203, Level = LogLevel.Error, Message = "Pedido {Reference} sem análise: o serviço respondeu {StatusCode} em {ElapsedMs} ms")]
    internal static partial void StatusRefused(this ILogger logger, string reference, int statusCode, long elapsedMs);
}
