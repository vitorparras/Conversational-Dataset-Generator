using System.Text.Json;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A package by id, version and content hash, read either from a lock file or from the folder the package cache extracted it to.</summary>
internal sealed record PackageIdentity(string Id, string Version, string ContentHash)
{
    private const string Framework = "net10.0";
    private const string CacheMetadata = ".nupkg.metadata";
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>The packages a lock file pins for the framework, by id; a missing lock file pins nothing.</summary>
    public static IReadOnlyDictionary<string, PackageIdentity> InLock(string lockPath)
    {
        var pinned = new Dictionary<string, PackageIdentity>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(lockPath))
        {
            return pinned;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
        var packages = document.RootElement.GetProperty("dependencies").GetProperty(Framework).EnumerateObject()
            .Where(entry => entry.Value.TryGetProperty("contentHash", out _));
        foreach (var entry in packages)
        {
            var resolved = entry.Value.GetProperty("resolved").GetString()!;
            pinned[entry.Name] = new PackageIdentity(entry.Name, resolved, entry.Value.GetProperty("contentHash").GetString()!);
        }

        return pinned;
    }

    /// <summary>The package of each analyzer that sits in one of the caches, by the analyzer's path: a cache keeps a package in
    /// <c>id/version/</c>, with the hash it checked on extraction beside it.</summary>
    public static IReadOnlyDictionary<string, PackageIdentity> InCache(IEnumerable<string> analyzerPaths, IEnumerable<string> packagesRoots) =>
        packagesRoots
            .SelectMany(root => analyzerPaths.Select(path => (Path: path, Root: root, Parts: Path.GetRelativePath(root, path).Split(Separators))))
            .Where(entry => entry.Parts.Length > 2 && entry.Parts[0] != ".." && !Path.IsPathRooted(entry.Parts[0]))
            .DistinctBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                entry => entry.Path,
                entry => new PackageIdentity(entry.Parts[0], entry.Parts[1], HashOf(Path.Combine(entry.Root, entry.Parts[0], entry.Parts[1], CacheMetadata))),
                StringComparer.OrdinalIgnoreCase);

    private static string HashOf(string metadataPath)
    {
        if (!File.Exists(metadataPath))
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
        return document.RootElement.TryGetProperty("contentHash", out var hash) ? hash.GetString() ?? string.Empty : string.Empty;
    }
}
