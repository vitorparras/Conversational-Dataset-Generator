using System.Diagnostics.CodeAnalysis;

namespace Core.Host;

/// <summary>What the periodic comparison writes: where the two layers differ, by role, table, commands, counts and steps, and why it could not run; never a statement or a value.</summary>
[SuppressMessage("ApiDesign", "RS0030", Justification = "The generated bodies are the one sanctioned caller of ILogger.Log.")]
internal static partial class AuditComparisonLogMessages
{
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Warning,
        Message = "Diferença nova entre o registro do servidor e a cadeia: papel {Role}, tabela {Table}, {Detail}")]
    internal static partial void NewDifference(this ILogger logger, string role, string table, string detail);

    [LoggerMessage(
        EventId = 202,
        Level = LogLevel.Warning,
        Message = "O arquivo {File} do registro do servidor termina numa linha cortada, o servidor caiu ao gravá-la: a linha ficou de fora da comparação e o arquivo não deve ser mexido")]
    internal static partial void CutOffFile(this ILogger logger, string file);

    [LoggerMessage(EventId = 201, Level = LogLevel.Error, Message = "A comparação do registro do servidor com a cadeia não pôde rodar: {Failure}")]
    internal static partial void ComparisonFailed(this ILogger logger, string failure);
}
