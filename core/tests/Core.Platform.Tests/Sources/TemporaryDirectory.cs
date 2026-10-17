namespace Core.Platform.Tests.Sources;

/// <summary>A fresh directory under the system's temporary path, removed with everything in it on dispose.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdg-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Writes the content under the name and returns the full path.</summary>
    public string WriteFile(string name, string content)
    {
        var path = System.IO.Path.Combine(Path, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
