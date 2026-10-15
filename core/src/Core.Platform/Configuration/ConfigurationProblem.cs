using System.Diagnostics;

namespace Core.Platform.Configuration;

/// <summary>One reason the configuration was refused: the key, what was found and what would be accepted.</summary>
internal sealed record ConfigurationProblem(
    ConfigurationProblemKind Kind,
    string Key,
    string? Found,
    IReadOnlyList<string>? Accepted)
{
    /// <summary>The refusal as the operator reads it.</summary>
    public string Describe() => Kind switch
    {
        ConfigurationProblemKind.UnknownKey => $"chave desconhecida '{Key}': nenhuma seção a declara",
        ConfigurationProblemKind.MissingKey =>
            $"chave exigida ausente '{Key}': nenhuma fonte a declara e ela não tem valor padrão",
        ConfigurationProblemKind.EmptyValue =>
            $"chave '{Key}' declarada sem valor: encontrado valor vazio; aceitos: {AcceptedText()}",
        ConfigurationProblemKind.ValueNotAccepted =>
            $"valor não aceito para '{Key}': encontrado {FoundText()}; aceitos: {AcceptedText()}",
        _ => throw new UnreachableException(),
    };

    private string AcceptedText() => Accepted is null ? "qualquer texto não vazio" : string.Join(", ", Accepted);

    private string FoundText() => Found is null ? "[valor sensível, não exibido]" : $"'{Found}'";
}
