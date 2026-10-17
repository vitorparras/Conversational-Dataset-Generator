namespace Core.Platform.Startup;

/// <summary>Checks that a file the installation must carry is where the configuration says it is.</summary>
public sealed class BundledFileCheck : IStartupCheck
{
    private readonly string configuredPath;
    private readonly string baseDirectory;

    /// <summary>The item is named by its configuration key; the path is read from the base directory.</summary>
    public BundledFileCheck(string name, string configuredPath, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        Name = name;
        this.configuredPath = configuredPath;
        this.baseDirectory = baseDirectory;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public Task<CheckResult> Run(CancellationToken cancellationToken) =>
        Task.FromResult(File.Exists(Path.Combine(baseDirectory, configuredPath)) ? Found() : Missing());

    private CheckResult Found() => CheckResult.Passed(Name, "recurso presente", configuredPath);

    private CheckResult Missing() => CheckResult.Failed(
        Name, $"recurso ausente: o arquivo declarado em '{Name}' não existe", configuredPath);
}
