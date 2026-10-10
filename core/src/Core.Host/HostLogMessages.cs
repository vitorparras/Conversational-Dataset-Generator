namespace Core.Host;

/// <summary>What the host writes about itself.</summary>
internal static partial class HostLogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Núcleo ouvindo em {Addresses}")]
    public static partial void Listening(this ILogger logger, string addresses);

    [LoggerMessage(EventId = 2, Level = LogLevel.Critical, Message = "Inicialização recusada: {Failed} conferência(s) reprovada(s); a porta não foi aberta")]
    public static partial void StartupRefused(this ILogger logger, int failed);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Encerramento pedido; requisições em andamento concluídas")]
    public static partial void ShutdownRequested(this ILogger logger);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Encerramento concluído; código de saída {ExitCode}")]
    public static partial void ShutdownCompleted(this ILogger logger, int exitCode);

    [LoggerMessage(EventId = 5, Level = LogLevel.Critical, Message = "Falha inesperada; o núcleo encerra com código de saída {ExitCode}")]
    public static partial void UnexpectedFailure(this ILogger logger, Exception exception, int exitCode);
}
