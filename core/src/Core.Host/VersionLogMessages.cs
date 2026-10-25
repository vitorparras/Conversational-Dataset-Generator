using System.Diagnostics.CodeAnalysis;
using Core.Platform.Versions;

namespace Core.Host;

/// <summary>What the host writes about the versions it runs under; versions and a commit, which are public.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class VersionLogMessages
{
    [LoggerMessage(EventId = 200, Level = LogLevel.Information, Message = "Versões em execução")]
    internal static partial void VersionsInForce(this ILogger logger, [LogProperties(OmitReferenceName = true)] VersionReport versions);
}
