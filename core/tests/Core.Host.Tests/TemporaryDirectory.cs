namespace Core.Host.Tests;

/// <summary>A fresh directory under the system's temporary path, removed with everything in it on dispose.</summary>
internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cdg-host-test-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
