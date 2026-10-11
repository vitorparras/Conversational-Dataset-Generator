using System.Globalization;
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

    private readonly bool absolute;
    private readonly string shown;
    private readonly string fullPath;
    private readonly ConfigurationOrigin origin;

    /// <summary>
    /// Checks <paramref name="declaredPath"/>, the entry at <paramref name="position"/> (zero-based)
    /// of the key, resolved against <paramref name="baseDirectory"/>.
    /// </summary>
    public BundledFileCheck(string declaredPath, int position, string baseDirectory, ConfigurationOrigin origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declaredPath);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentNullException.ThrowIfNull(baseDirectory);
        // What the log and the status show: a relative path as declared; of an absolute one, only the
        // file name. An absolute path is accepted (it may point at another volume), but it carries
        // the machine's folders and, under a user profile, the account name, and never reaches the
        // log (requirement 021); the key named in the same line says where to look. The resolved path
        // is never shown either: it carries the installation folder.
        absolute = Path.IsPathRooted(declaredPath);
        shown = absolute ? Path.GetFileName(declaredPath) : declaredPath;

        // The position in the key, as the configuration path of .NET writes an array element, tells
        // apart two entries that show the same file name: the operator knows which one is missing.
        Name = string.Create(CultureInfo.InvariantCulture, $"{BundledFiles}[{position}]: {shown}");
        fullPath = Path.GetFullPath(declaredPath, baseDirectory);
        this.origin = origin;
    }

    /// <summary>The keys the installation declares.</summary>
    public static ConfigurationSection Section { get; } = new(
        "Installation",
        [ConfigurationKey.WithDefault("BundledFiles", string.Empty)]);

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>One check per file the configuration declares, in the order declared; empty entries are not counted.</summary>
    public static IReadOnlyList<IStartupCheck> FromConfiguration(EffectiveConfiguration configuration, string baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var origin = configuration.OriginOf(BundledFiles);
        return [.. configuration.Get(BundledFiles)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((path, position) => new BundledFileCheck(path, position, baseDirectory, origin))];
    }

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var place = absolute ? $"'{shown}', no caminho absoluto" : $"'{shown}' na pasta do executável";
        var expectedSource =
            $"acompanha a instalação, em {place}, declarado em {BundledFiles} ({ConfigurationProblem.OriginName(origin)})";
        var result = File.Exists(fullPath)
            ? CheckResult.Pass(Name, "presente", expectedSource)
            : CheckResult.Fail(Name, $"recurso ausente '{shown}'", expectedSource);
        return Task.FromResult(result);
    }
}
