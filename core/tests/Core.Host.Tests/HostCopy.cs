namespace Core.Host.Tests;

/// <summary>A copy of the built host in a fresh directory, with the settings file given beside the binary.</summary>
internal sealed class HostCopy : IDisposable
{
    private const string SettingsFile = "appsettings.json";
    private readonly TemporaryDirectory directory = new();

    public HostCopy(string settingsJson)
    {
        foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory))
        {
            File.Copy(file, System.IO.Path.Combine(directory.Path, System.IO.Path.GetFileName(file)));
        }

        File.WriteAllText(System.IO.Path.Combine(directory.Path, SettingsFile), settingsJson);
    }

    public string Path => directory.Path;

    /// <summary>The copy started from its own directory with the arguments given.</summary>
    public HostInstallation Installation(params IReadOnlyList<string> arguments) => new(Path, Path, arguments);

    public void Dispose() => directory.Dispose();
}
