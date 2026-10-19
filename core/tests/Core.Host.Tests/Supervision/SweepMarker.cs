namespace Core.Host.Tests.Supervision;

/// <summary>A file created at the start of a test, whose own time of writing is the moment the sweep reads from, so the file system's clock is the only one compared.</summary>
internal sealed class SweepMarker : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"cdg-sweep-marker-{Guid.NewGuid():N}");

    public SweepMarker()
    {
        File.WriteAllText(path, "marker");
        Since = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
    }

    public DateTimeOffset Since { get; }

    public void Dispose() => File.Delete(path);
}
