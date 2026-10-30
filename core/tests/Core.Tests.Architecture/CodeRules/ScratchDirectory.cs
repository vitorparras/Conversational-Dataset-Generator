namespace Core.Tests.Architecture.CodeRules;

/// <summary>A temporary directory that is deleted on disposal, holding the <c>global.json</c> of the repository so that a <c>dotnet</c> run inside it uses the SDK the repository pins.</summary>
internal sealed class ScratchDirectory : IDisposable
{
    public const string GlobalJson = "global.json";

    private ScratchDirectory(string path)
    {
        Path = path;
        _ = Directory.CreateDirectory(path);
        File.Copy(RootGlobalJson, System.IO.Path.Combine(path, GlobalJson));
    }

    /// <summary>The <c>global.json</c> at the root of the repository, which every scratch directory is given a copy of.</summary>
    public static string RootGlobalJson => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SourceTree.CoreRoot)!, GlobalJson);

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
