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
/// that holds no analyzer of that name with other contents, or a new one. Inside one build the same holds: the
/// paths it asks for are laid out in layers, the first content of each name in the first layer, the second content
/// of a name in the second, and so on, and each layer takes its own context, so that both analyzers of one name
/// load and the inspection sees the diagnostics of both. An analyzer asked for twice with one content, under two
/// paths, is read once and is one assembly. A path that cannot be loaded is a failed load, and the inspection
/// reports that with the file.
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

    /// <summary>
    /// The loader for one build's analyzers: each path goes to the context of its layer, which is the first that can hold the whole layer beside
    /// what it already holds, and the loader answers a path with the assembly its context loaded.
    /// </summary>
    public static IAnalyzerAssemblyLoader For(IEnumerable<string> analyzerPaths)
    {
        var layers = LayersOf(analyzerPaths);
        lock (PoolGate)
        {
            var byPath = new Dictionary<string, AnalyzerLoader>(StringComparer.OrdinalIgnoreCase);
            foreach (var layer in layers)
            {
                var loader = Pool.Find(candidate => candidate.Accepts(layer.Contents));
                if (loader is null)
                {
                    loader = new AnalyzerLoader();
                    Pool.Add(loader);
                }

                loader.Claim(layer.Contents);
                layer.Paths.ForEach(path => byPath[path] = loader);
            }

            return new BuildLoader(byPath);
        }
    }

    /// <summary>
    /// The paths of a build by layer: the k-th different content of a name is in layer k, so no layer holds two contents of one name, and a content
    /// asked for under several paths is in one layer, with all of them.
    /// </summary>
    private static List<Layer> LayersOf(IEnumerable<string> analyzerPaths)
    {
        var layers = new List<Layer>();
        var layerOf = new Dictionary<(string Name, string Content), int>();
        var contentsOfName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in analyzerPaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var content = ContentOf(path);
            if (!layerOf.TryGetValue((name.ToUpperInvariant(), content), out var index))
            {
                index = contentsOfName.GetValueOrDefault(name);
                contentsOfName[name] = index + 1;
                layerOf[(name.ToUpperInvariant(), content)] = index;
            }

            while (layers.Count <= index)
            {
                layers.Add(new Layer());
            }

            layers[index].Contents[name] = content;
            layers[index].Paths.Add(path);
        }

        return layers;
    }

    private static string ContentOf(string path) =>
        File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : string.Empty;

    private bool Accepts(IReadOnlyDictionary<string, string> wanted) =>
        wanted.All(entry => !contentByName.TryGetValue(entry.Key, out var held) || held == entry.Value);

    private void Claim(IReadOnlyDictionary<string, string> wanted)
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

    /// <summary>The analyzers of one layer of a build: their names with their contents, and the paths they were asked for under.</summary>
    private sealed class Layer
    {
        internal Dictionary<string, string> Contents { get; } = new(StringComparer.OrdinalIgnoreCase);

        internal List<string> Paths { get; } = [];
    }

    /// <summary>What a build is handed as its loader: each analyzer path is loaded by the context of its layer, and a dependency location is known to all of the build's contexts.</summary>
    private sealed class BuildLoader : IAnalyzerAssemblyLoader
    {
        private readonly Dictionary<string, AnalyzerLoader> byPath;

        internal BuildLoader(Dictionary<string, AnalyzerLoader> byPath)
        {
            this.byPath = byPath;
        }

        public void AddDependencyLocation(string fullPath) => byPath.Values.Distinct().ToList().ForEach(loader => loader.AddDependencyLocation(fullPath));

        public Assembly LoadFromPath(string fullPath) =>
            byPath.TryGetValue(Path.GetFullPath(fullPath), out var loader)
                ? loader.LoadFromPath(fullPath)
                : throw new InvalidOperationException($"{fullPath} is not one of the analyzers this build asked for.");
    }
}
