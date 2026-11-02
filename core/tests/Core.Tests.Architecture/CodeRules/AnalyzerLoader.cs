using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// Loads analyzer assemblies from bytes, so that nothing stays locked on disk. There is one loader for the whole test
/// process and its context is never collected: each analyzer is read once, by content, and loading and unloading
/// contexts while other tests allocate was what the runtime's collector did not survive. Two analyzers of one name
/// and different contents cannot share a context, and builds of different projects can legitimately carry them (the
/// SDK pack and the package cache each bring their own copy of a generator), so a build takes the first context
/// that holds no analyzer of that name with other contents, or a new one. Inside one build the conflict stays a
/// failed load, and the inspection reports that.
/// </summary>
internal sealed class AnalyzerLoader : AssemblyLoadContext, IAnalyzerAssemblyLoader
{
    private readonly Lock gate = new();
    private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> loadedByContent = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> contentByName = new(StringComparer.OrdinalIgnoreCase);

    private AnalyzerLoader()
        : base("analyzers", isCollectible: false)
    {
    }

    private static readonly Lock PoolGate = new();
    private static readonly List<AnalyzerLoader> Pool = [];

    /// <summary>The context for one build's analyzers: the first that can hold them all beside what it already holds.</summary>
    public static AnalyzerLoader For(IEnumerable<string> analyzerPaths)
    {
        var wanted = analyzerPaths
            .DistinctBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), ContentOf, StringComparer.OrdinalIgnoreCase);
        lock (PoolGate)
        {
            var loader = Pool.Find(candidate => candidate.Accepts(wanted));
            if (loader is null)
            {
                loader = new AnalyzerLoader();
                Pool.Add(loader);
            }

            loader.Claim(wanted);
            return loader;
        }
    }

    private static string ContentOf(string path) =>
        File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : string.Empty;

    private bool Accepts(Dictionary<string, string> wanted) =>
        wanted.All(entry => !contentByName.TryGetValue(entry.Key, out var held) || held == entry.Value);

    private void Claim(Dictionary<string, string> wanted)
    {
        foreach (var entry in wanted)
        {
            contentByName[entry.Key] = entry.Value;
        }
    }

    public void AddDependencyLocation(string fullPath)
    {
        lock (gate)
        {
            _ = directories.Add(Path.GetDirectoryName(fullPath)!);
        }
    }

    public Assembly LoadFromPath(string fullPath)
    {
        lock (gate)
        {
            _ = directories.Add(Path.GetDirectoryName(fullPath)!);
            return LoadBytes(fullPath);
        }
    }

    /// <summary>The dependency from the analyzers' directories; null when the test process already has it (the compiler's own assemblies), so an analyzer shares the compiler's types.</summary>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (Default.Assemblies.Any(loaded => string.Equals(loaded.GetName().Name, assemblyName.Name, StringComparison.Ordinal)))
        {
            return null;
        }

        lock (gate)
        {
            return directories.Select(directory => Path.Combine(directory, assemblyName.Name + ".dll")).Where(File.Exists).Select(LoadBytes).FirstOrDefault();
        }
    }

    private Assembly LoadBytes(string path)
    {
        var image = File.ReadAllBytes(path);
        var content = Convert.ToHexString(SHA256.HashData(image));
        if (!loadedByContent.TryGetValue(content, out var assembly))
        {
            loadedByContent[content] = assembly = LoadFromStream(new MemoryStream(image));
        }

        return assembly;
    }
}
