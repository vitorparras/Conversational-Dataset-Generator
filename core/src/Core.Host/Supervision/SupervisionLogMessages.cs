using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Supervision;

/// <summary>What the supervision writes about the language service; a process number, a port, an exit code and a reference, never the secret.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SupervisionLogMessages
{
    [LoggerMessage(EventId = 100, Level = LogLevel.Information, Message = "Serviço de linguagem iniciado (processo {ProcessId}, porta {Port})")]
    internal static partial void ServiceStarted(this ILogger logger, int processId, int port);

    [LoggerMessage(EventId = 101, Level = LogLevel.Error, Message = "Serviço de linguagem não ficou de pé: {Outcome}")]
    internal static partial void ServiceNotReady(this ILogger logger, StartOutcome outcome);

    [LoggerMessage(EventId = 110, Level = LogLevel.Information, Message = "Serviço de linguagem encerrado (processo {ProcessId})")]
    internal static partial void ServiceStopped(this ILogger logger, int processId);

    [LoggerMessage(EventId = 102, Level = LogLevel.Error, Message = "Serviço de linguagem encerrou (processo {ProcessId}, código {ExitCode}); o núcleo não está apto")]
    internal static partial void ServiceLost(this ILogger logger, int processId, int exitCode);

    [LoggerMessage(EventId = 103, Level = LogLevel.Information, Message = "Reinício do serviço de linguagem pedido por {Requester} (referência {Reference})")]
    internal static partial void RestartRequested(this ILogger logger, RestartRequester requester, Guid reference);

    [LoggerMessage(EventId = 104, Level = LogLevel.Information, Message = "Reinício do serviço de linguagem terminou com o serviço {State} (referência {Reference})")]
    internal static partial void RestartEnded(this ILogger logger, ServiceState state, Guid reference);
}
