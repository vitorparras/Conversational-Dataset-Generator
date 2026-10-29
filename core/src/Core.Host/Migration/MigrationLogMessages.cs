using System.Diagnostics.CodeAnalysis;

namespace Core.Host.Migration;

/// <summary>What the migration mode writes: step names, versions, digests and a SQLSTATE, never the connection, the password or the server's own text.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class MigrationLogMessages
{
    [LoggerMessage(EventId = 300, Level = LogLevel.Information, Message = "Passo {Step} aplicado e registrado")]
    internal static partial void StepApplied(this ILogger logger, string step);

    [LoggerMessage(EventId = 301, Level = LogLevel.Information, Message = "Esquema migrado até a versão {Version}")]
    internal static partial void SchemaMigrated(this ILogger logger, int version);

    [LoggerMessage(EventId = 302, Level = LogLevel.Information, Message = "Nada a migrar: o esquema já está na versão {Version}")]
    internal static partial void NothingToMigrate(this ILogger logger, int version);

    [LoggerMessage(EventId = 303, Level = LogLevel.Error, Message = "Migração recusada: o esquema está na versão {Found}, à frente da {Expected} que esta instalação conhece; nada foi aplicado")]
    internal static partial void RefusedAhead(this ILogger logger, int found, int expected);

    [LoggerMessage(EventId = 304, Level = LogLevel.Error, Message = "Migração recusada: o passo {Step} foi aplicado com conteúdo diferente do atual "
        + "(registrado {Registered}, atual {Current}); nada foi aplicado")]
    internal static partial void RefusedDivergent(this ILogger logger, string step, string registered, string current);

    [LoggerMessage(EventId = 305, Level = LogLevel.Error, Message = "Migração falhou no passo {Step} (SQLSTATE {SqlState}); o passo foi desfeito e o esquema ficou na versão {Version}")]
    internal static partial void StepFailed(this ILogger logger, string step, string sqlState, int version);

    [LoggerMessage(EventId = 306, Level = LogLevel.Error, Message = "Migração não rodou: o servidor não aceitou a conexão ou a leitura do registro (SQLSTATE {SqlState}); "
        + "confira o endereço, o banco, o papel e a senha de dono")]
    internal static partial void NotConnected(this ILogger logger, string sqlState);

    [LoggerMessage(EventId = 307, Level = LogLevel.Information, Message = "Falha do passo {Step} gravada em schema_failure (versão, nome, resumo e SQLSTATE; sem texto do passo)")]
    internal static partial void FailureRecorded(this ILogger logger, string step);
}
