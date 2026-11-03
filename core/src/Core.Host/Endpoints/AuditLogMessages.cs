using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Endpoints;

/// <summary>What the audit route writes when it cannot read the server's log: the file and the type of the failure, never a line of the log nor the failure's own text.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class AuditLogMessages
{
    [LoggerMessage(EventId = 310, Level = LogLevel.Error, Message = "Leitura do registro do servidor falhou no arquivo {File}: {FailureType}; a resposta foi 503")]
    internal static partial void Unreadable(this ILogger logger, string file, string failureType);
}
