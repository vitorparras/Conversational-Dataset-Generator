using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Endpoints;

/// <summary>What the host writes when a page failed: the kind of failure alone, because its message may carry text of a request.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class ScreenFailureLogMessages
{
    [LoggerMessage(
        EventId = 341,
        Level = LogLevel.Error,
        Message = "Uma página falhou ao ser montada ({Kind}); a resposta foi a página de falha, sem detalhe, com o código {Reference}")]
    internal static partial void ScreenFailed(this ILogger logger, string? kind, string reference);
}
