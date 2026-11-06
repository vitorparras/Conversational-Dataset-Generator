using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;

namespace Core.Host;

/// <summary>What the host writes about an entry it could not record: how the chain answered and the prefix of the origin, nothing the person typed.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SignInLogMessages
{
    [LoggerMessage(
        EventId = 340,
        Level = LogLevel.Warning,
        Message = "Entrada recusada que não chegou à cadeia ({Outcome}), prefixo {Network}; nada foi gravado")]
    internal static partial void RefusalNotRecorded(this ILogger logger, CustodyOutcome outcome, string network);
}
