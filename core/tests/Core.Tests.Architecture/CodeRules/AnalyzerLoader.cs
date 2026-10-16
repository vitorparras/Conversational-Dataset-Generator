using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Loads analyzer assemblies from bytes into a context of their own, so that nothing stays locked on disk and the context can be dropped.</summary>
internal sealed class AnalyzerLoader : AssemblyLoadContext, IAnalyzerAssemblyLoader
{
    private readonly HashSet<string> directories = new(StringComparer.OrdinalIgnoreCase);

    public AnalyzerLoader()
        : base(isCollectible: true)
    {
    }

    public void AddDependencyLocation(string fullPath) => directories.Add(Path.GetDirectoryName(fullPath)!);

    public Assembly LoadFromPath(string fullPath)
    {
        _ = directories.Add(Path.GetDirectoryName(fullPath)!);
        return LoadBytes(fullPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName) =>
        directories.Select(directory => Path.Combine(directory, assemblyName.Name + ".dll")).Where(File.Exists).Select(LoadBytes).FirstOrDefault();

    private Assembly LoadBytes(string path) => LoadFromStream(new MemoryStream(File.ReadAllBytes(path)));
}
