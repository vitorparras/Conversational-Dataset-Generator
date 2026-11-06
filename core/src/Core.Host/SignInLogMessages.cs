using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Custody;
using Core.Persistence.People;

namespace Core.Host;

/// <summary>What the host writes about an entry it could not record: the reason and the prefix of the origin, never the name tried nor the password.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class SignInLogMessages
{
    [LoggerMessage(
        EventId = 340,
        Level = LogLevel.Warning,
        Message = "Entrada recusada ({Refusal}) que não chegou à cadeia ({Outcome}), prefixo {Network}; nada foi gravado")]
    internal static partial void RefusalNotRecorded(this ILogger logger, SignInRefusal refusal, CustodyOutcome outcome, string network);
}
