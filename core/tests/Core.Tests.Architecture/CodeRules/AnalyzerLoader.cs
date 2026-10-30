using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// Loads analyzer assemblies from bytes, so that nothing stays locked on disk. There is one loader for the whole test
/// process and its context is never collected: each analyzer is read once, by content, and loading and unloading
/// contexts while other tests allocate was what the runtime's collector did not survive. Two analyzers of one identity
/// and different contents cannot share it; the load then fails and the inspection reports that.
/// </summary>
internal sealed class AnalyzerLoader : AssemblyLoadContext, IAnalyzerAssemblyLoader
{
    private readonly Lock gate = new();
    private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Assembly> loadedByContent = new(StringComparer.Ordinal);

    private AnalyzerLoader()
        : base("analyzers", isCollectible: false)
    {
    }

    public static AnalyzerLoader Shared { get; } = new();

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
