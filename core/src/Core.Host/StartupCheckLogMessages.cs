using System.Diagnostics.CodeAnalysis;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>
/// What the host writes about each check of the startup, once the checks end: its name and what it concluded. Never its message nor where it looked, which may name a path of the
/// machine or a material of the installation; the detail stays behind a session, in <c>/status</c> (#632).
/// </summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class StartupCheckLogMessages
{
    [LoggerMessage(EventId = 342, Level = LogLevel.Information, Message = "Conferência da subida {Check}: {Outcome}")]
    internal static partial void CheckConcluded(this ILogger logger, string check, CheckStatus outcome);
}
