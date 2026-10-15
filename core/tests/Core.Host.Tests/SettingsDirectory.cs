namespace Core.Host.Tests;

/// <summary>A fresh working directory holding one settings file, removed with everything in it on dispose.</summary>
internal sealed class SettingsDirectory : IDisposable
{
    private const string FileName = "appsettings.json";

    public SettingsDirectory(string settingsJson)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdg-host-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path);
        File.WriteAllText(System.IO.Path.Combine(Path, FileName), settingsJson);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
