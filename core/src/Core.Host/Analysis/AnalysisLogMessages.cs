using System.Diagnostics.CodeAnalysis;
using Core.Boundary.Contracts;

namespace Core.Host.Analysis;

/// <summary>What the analysis writes about a material: its opaque reference, the reason it stopped and the reference it resumes; never the text.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class AnalysisLogMessages
{
    [LoggerMessage(EventId = 204, Level = LogLevel.Error, Message = "Análise {Reference} interrompida: {Reason}; nada foi gravado")]
    internal static partial void Interrupted(this ILogger logger, Guid reference, UnavailabilityReason reason);

    [LoggerMessage(EventId = 205, Level = LogLevel.Information, Message = "Análise {Reference} pedida para retomar a análise interrompida {Interrupted}")]
    internal static partial void Resumed(this ILogger logger, Guid reference, Guid interrupted);
}
