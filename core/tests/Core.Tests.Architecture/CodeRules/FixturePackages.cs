using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Builds the analyzer packages a fixture needs from its sources, into a local feed inside the fixture's temporary tree.</summary>
internal static class FixturePackages
{
    public const string SourcesDirectory = "package-sources";
    public const string FeedDirectory = "feed";
    private const string Version = "1.0.0";
    private static readonly string[] CompilerAssemblyNames = ["Microsoft.CodeAnalysis.dll", "Microsoft.CodeAnalysis.CSharp.dll"];

    /// <summary>Every <c>Id.cs.txt</c> under the sources directory becomes <c>feed/Id.1.0.0.nupkg</c>, an analyzer package whose assembly is that source compiled.</summary>
    public static void Build(string tree)
    {
        var sources = Path.Combine(tree, SourcesDirectory);
        if (!Directory.Exists(sources))
        {
            return;
        }

        var feed = Directory.CreateDirectory(Path.Combine(tree, FeedDirectory)).FullName;
        foreach (var source in Directory.EnumerateFiles(sources, "*.cs.txt"))
        {
            var id = Path.GetFileName(source)[..^".cs.txt".Length];
            Pack(feed, id, Compile(id, File.ReadAllText(source)));
        }
    }

    /// <summary>
    /// The assembly takes a name of its own from the package id and the source, because two fixtures that give one id to
    /// different sources must not be one identity, which a single loader cannot hold twice. The file keeps the package id, and the build is deterministic so one source is one content.
    /// </summary>
    private static byte[] Compile(string id, string source)
    {
        var compilation = CSharpCompilation.Create(
            $"{id}.{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source)))[..8]}",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        return result.Success
            ? stream.ToArray()
            : throw new InvalidOperationException($"The fixture package {id} does not compile: {string.Join("; ", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");
    }

    private static void Pack(string feed, string id, byte[] assembly)
    {
        using var package = ZipFile.Open(Path.Combine(feed, $"{id}.{Version}.nupkg"), ZipArchiveMode.Create);
        Add(package, $"{id}.nuspec", System.Text.Encoding.UTF8.GetBytes(Nuspec(id)));
        Add(package, $"analyzers/dotnet/cs/{id}.dll", assembly);
    }

    private static void Add(ZipArchive package, string name, byte[] content)
    {
        using var entry = package.CreateEntry(name).Open();
        entry.Write(content);
    }

    private static string Nuspec(string id) =>
        $"""<?xml version="1.0"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">""" +
        $"<metadata><id>{id}</id><version>{Version}</version><authors>fixture</authors><description>fixture</description></metadata></package>";

    /// <summary>The runtime's assemblies, with the compiler's own taken from the SDK that will load the package, so
    /// that the package never references a newer compiler than the one that runs it.</summary>
    private static IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
            .Concat(SdkCompilerAssemblies())
            .Select(path => MetadataReference.CreateFromFile(path));

    private static IEnumerable<string> SdkCompilerAssemblies() =>
        CompilerAssemblyNames.Select(name => Path.Combine(ToolLocations.SdkDirectory, "Roslyn", "bincore", name));
}
