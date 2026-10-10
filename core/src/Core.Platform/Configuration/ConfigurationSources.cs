using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace Core.Platform.Configuration;

/// <summary>A configuration source and the origin its values are reported with.</summary>
/// <param name="Origin">The origin, which also fixes its precedence.</param>
/// <param name="Name">How the source is named to the operator.</param>
/// <param name="Source">The platform source that reads the values.</param>
public sealed record OriginatedSource(ConfigurationOrigin Origin, string Name, IConfigurationSource Source);

/// <summary>The three sources the process reads, in the form the operator writes them.</summary>
public static class ConfigurationSources
{
    /// <summary>The file read from the directory of the executable.</summary>
    public const string FileName = "appsettings.json";

    /// <summary>The prefix of the environment variables read; <c>__</c> separates the path segments.</summary>
    public const string EnvironmentPrefix = "CDG__";

    /// <summary>The file, the prefixed environment variables, and the arguments.</summary>
    public static IReadOnlyList<OriginatedSource> Standard(string directory, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(arguments);
        return
        [
            JsonFile(Path.Combine(directory, FileName)),
            new(
                ConfigurationOrigin.Environment,
                EnvironmentPrefix + "*",
                new EnvironmentVariablesConfigurationSource { Prefix = EnvironmentPrefix }),
            Arguments(arguments),
        ];
    }

    /// <summary>A JSON file; a file that does not exist declares nothing.</summary>
    public static OriginatedSource JsonFile(string path)
    {
        var source = new JsonConfigurationSource { Path = path, Optional = true, ReloadOnChange = false };
        source.ResolveFileProvider();
        return new(ConfigurationOrigin.File, Path.GetFileName(path), source);
    }

    /// <summary>Arguments of the form <c>--Section:Key=value</c> or <c>--Section:Key value</c>.</summary>
    public static OriginatedSource Arguments(IReadOnlyList<string> arguments) =>
        new(ConfigurationOrigin.Argument, "linha de comando", new StrictArgumentsSource(arguments));
}
