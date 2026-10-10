namespace Core.Platform.Configuration;

/// <summary>Why loading refused a key.</summary>
public enum ConfigurationProblemKind
{
    /// <summary>A source declares a key no section declares.</summary>
    UnknownKey = 0,

    /// <summary>A required key that no source declares.</summary>
    MissingKey = 1,

    /// <summary>A value the key does not accept.</summary>
    ValueNotAccepted = 2,

    /// <summary>A source that could not be read.</summary>
    UnreadableSource = 3,
}

/// <summary>One reason the configuration refuses to load, naming the key involved.</summary>
/// <param name="Kind">Why the key was refused.</param>
/// <param name="Key">The key path, as the source wrote it or as the section declares it.</param>
/// <param name="Origin">The source that brought the value, when there is one.</param>
/// <param name="Found">The value found; null when absent or when the key is sensitive.</param>
/// <param name="Accepted">What the key would accept, for a refused value.</param>
public sealed record ConfigurationProblem(
    ConfigurationProblemKind Kind,
    string Key,
    ConfigurationOrigin? Origin,
    string? Found,
    string? Accepted)
{
    /// <summary>The refusal as the operator reads it.</summary>
    public string Describe() => Kind switch
    {
        ConfigurationProblemKind.UnknownKey =>
            $"chave desconhecida '{Key}' ({OriginName(Origin)}): nenhuma seção a declara",
        ConfigurationProblemKind.MissingKey =>
            $"chave exigida ausente '{Key}': nenhuma fonte a declara, e ela não tem valor padrão",
        ConfigurationProblemKind.ValueNotAccepted =>
            $"valor não aceito para '{Key}' ({OriginName(Origin)}): encontrado {FoundText()}; aceitos: {Accepted}",
        ConfigurationProblemKind.UnreadableSource =>
            $"fonte de configuração ilegível '{Key}' ({OriginName(Origin)}): {Found}",
        _ => throw new InvalidOperationException($"unknown problem kind {Kind}"),
    };

    /// <summary>The origin as the operator reads it.</summary>
    public static string OriginName(ConfigurationOrigin? origin) => origin switch
    {
        ConfigurationOrigin.Default => "padrão declarado",
        ConfigurationOrigin.File => "arquivo",
        ConfigurationOrigin.Environment => "ambiente",
        ConfigurationOrigin.Argument => "argumento",
        null => "sem origem",
        _ => throw new InvalidOperationException($"unknown origin {origin}"),
    };

    private string FoundText() => Found is null ? "[valor sensível, não exibido]" : $"'{Found}'";
}
