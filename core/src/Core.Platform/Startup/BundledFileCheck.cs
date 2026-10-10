using Core.Platform.Configuration;

namespace Core.Platform.Startup;

/// <summary>
/// Checks that a file declared as travelling with the installation is there (issue 26 CA-2 and
/// CA-4): a resource needed only in the middle of processing is checked here, at startup.
/// </summary>
public sealed class BundledFileCheck : IStartupCheck
{
    /// <summary>The declared files, separated by <c>;</c>; relative paths are next to the executable.</summary>
    public const string BundledFiles = "Installation:BundledFiles";

    private readonly string declaredPath;
    private readonly string fullPath;
    private readonly ConfigurationOrigin origin;

    /// <summary>Checks <paramref name="declaredPath"/>, resolved against <paramref name="baseDirectory"/>.</summary>
    public BundledFileCheck(string declaredPath, string baseDirectory, ConfigurationOrigin origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredPath);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        this.declaredPath = declaredPath;
        fullPath = Path.GetFullPath(declaredPath, baseDirectory);
        this.origin = origin;
    }

    /// <summary>The keys the installation declares.</summary>
    public static ConfigurationSection Section { get; } = new(
        "Installation",
        [ConfigurationKey.WithDefault("BundledFiles", string.Empty)]);

    /// <inheritdoc />
    public string Name => "bundled-file:" + declaredPath;

    /// <summary>One check per file the configuration declares, in the order declared.</summary>
    public static IReadOnlyList<IStartupCheck> FromConfiguration(EffectiveConfiguration configuration, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var origin = configuration.OriginOf(BundledFiles);
        return [.. configuration.Get(BundledFiles)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(path => new BundledFileCheck(path, baseDirectory, origin))];
    }

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var expectedSource =
            $"acompanha a instalação, em {fullPath}, declarado em {BundledFiles} ({ConfigurationProblem.OriginName(origin)})";
        var result = File.Exists(fullPath)
            ? CheckResult.Pass(Name, "presente", expectedSource)
            : CheckResult.Fail(Name, $"recurso ausente '{declaredPath}'", expectedSource);
        return Task.FromResult(result);
    }
}
