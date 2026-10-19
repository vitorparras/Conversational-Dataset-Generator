using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Supervision;

/// <summary>What the supervision writes about the language service; a process number and a port, never the secret.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SupervisionLogMessages
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Serviço de linguagem iniciado (processo {ProcessId}, porta {Port})")]
    internal static partial void ServiceStarted(this ILogger logger, int processId, int port);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error, Message = "Serviço de linguagem não ficou de pé: {Outcome}")]
    internal static partial void ServiceNotReady(this ILogger logger, StartOutcome outcome);
}
