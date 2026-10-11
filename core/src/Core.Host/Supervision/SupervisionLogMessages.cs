namespace Core.Host.Supervision;

/// <summary>What the supervision writes about the language service. Never the secret, never a path.</summary>
internal static partial class SupervisionLogMessages
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Information, Message = "Serviço de linguagem iniciado (processo {ProcessId}, porta {Port})")]
    public static partial void ServiceStarted(this ILogger logger, int processId, int port);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information, Message = "Serviço de linguagem respondeu à conferência de saúde")]
    public static partial void ServiceHealthy(this ILogger logger);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Serviço de linguagem não ficou de pé: {Reason}")]
    public static partial void ServiceNotStarted(this ILogger logger, string reason);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Serviço de linguagem encerrou por conta própria (processo {ProcessId}, código de saída {ExitCode}); as chamadas seguintes dão indisponível")]
    public static partial void ServiceExited(this ILogger logger, int processId, int exitCode);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning, Message = "Reinício do serviço de linguagem pedido; o segredo anterior deixa de valer")]
    public static partial void ServiceRestartRequested(this ILogger logger);

    [LoggerMessage(EventId = 3005, Level = LogLevel.Information, Message = "Serviço de linguagem encerrado pelo núcleo (processo {ProcessId})")]
    public static partial void ServiceStopped(this ILogger logger, int processId);
}
