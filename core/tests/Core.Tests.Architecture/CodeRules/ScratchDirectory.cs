namespace Core.Tests.Architecture.CodeRules;

/// <summary>A temporary directory that is deleted on disposal.</summary>
internal sealed class ScratchDirectory : IDisposable
{
    private ScratchDirectory(string path)
    {
        Path = path;
        _ = Directory.CreateDirectory(path);
    }

    public string Path { get; }

    public static ScratchDirectory Create() =>
        new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "scratch-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Copies a fixture directory below <paramref name="relativeTarget"/>; a <c>.csproj.txt</c> file becomes a project file.</summary>
    public string CopyFixture(string fixtureDirectory, string relativeTarget)
    {
        var target = System.IO.Path.Combine(Path, relativeTarget);
        foreach (var file in Directory.EnumerateFiles(fixtureDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = System.IO.Path.GetRelativePath(fixtureDirectory, file).Replace(".csproj.txt", ".csproj", StringComparison.Ordinal);
            var destination = System.IO.Path.Combine(target, relative);
            _ = Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }

        return target;
    }

    /// <summary>Deletes a directory if it is there; a deletion that fails stops the test and names the directory.</summary>
    public static void Remove(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException exception)
        {
            throw new IOException($"The temporary directory {path} could not be deleted.", exception);
        }
    }

    public void Dispose() => Remove(Path);
}
