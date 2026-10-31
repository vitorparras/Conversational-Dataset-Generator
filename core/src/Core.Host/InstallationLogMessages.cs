using System.Diagnostics.CodeAnalysis;
using Core.Platform;

namespace Core.Host;

/// <summary>What the host writes about the kind of installation it is, once, when it starts; a demonstration is a warning so that no one takes it for a real one.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class InstallationLogMessages
{
    [LoggerMessage(EventId = 201, Level = LogLevel.Information, Message = "Instalação real")]
    internal static partial void RealInstallation(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] Installation installation,
        [LogProperties(OmitReferenceName = true)] CustodyDeclaration custody);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "Instalação de demonstração: roda sem cópia de guarda da chave e sem recuperação testada; não ingira material real")]
    internal static partial void DemonstrationInstallation(this ILogger logger, [LogProperties(OmitReferenceName = true)] Installation installation);
}
