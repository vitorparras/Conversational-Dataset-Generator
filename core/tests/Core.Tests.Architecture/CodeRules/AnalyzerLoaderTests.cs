using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The loader of the analyzers a build is given does not fail on two analyzers of one name (issue #501): with different contents both are loaded, each in a context of its
/// own, and the inspection sees the diagnostics of both; with the same content, under two paths, the analyzer is read once and is one assembly.
/// </summary>
public sealed class AnalyzerLoaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"zqx-analyzers-{Guid.NewGuid():N}");

    [Fact]
    public void TwoAnalyzersOfOneNameAndDifferentContentsInOneBuildLoadWithoutAnExceptionAndSeparately()
    {
        var name = NewName();
        var first = Analyzer(name, "first", "ZQX0001");
        var second = Analyzer(name, "second", "ZQX0002");

        var loader = AnalyzerLoader.For([first, second]);
        var loadedFirst = loader.LoadFromPath(first);
        var loadedSecond = loader.LoadFromPath(second);

        Assert.NotSame(loadedFirst, loadedSecond);
        Assert.Equal("ZQX0001", Assert.Single(Ids(loadedFirst)));
        Assert.Equal("ZQX0002", Assert.Single(Ids(loadedSecond)));
    }

    [Fact]
    public void TheInspectionSeesTheDiagnosticsOfBothAnalyzersOfOneNameAndReportsNoFailure()
    {
        var name = NewName();
        var first = Analyzer(name, "first", "ZQX0003");
        var second = Analyzer(name, "second", "ZQX0004");

        var findings = AnalyzerInspector.Inspect([first, second], Input());

        Assert.Empty(findings.Failures);
        Assert.Equal(["ZQX0003"], findings.DiagnosticsByAnalyzer[first]);
        Assert.Equal(["ZQX0004"], findings.DiagnosticsByAnalyzer[second]);
    }

    [Fact]
    public void TheSameNameAndTheSameContentUnderTwoPathsIsLoadedOnce()
    {
        var name = NewName();
        var first = Analyzer(name, "first", "ZQX0005");
        var second = Copy(first, "second");

        var loader = AnalyzerLoader.For([first, second]);

        Assert.Same(loader.LoadFromPath(first), loader.LoadFromPath(second));
    }

    [Fact]
    public void TwoBuildsThatCarryDifferentContentsOfOneNameStillLoadBoth()
    {
        var name = NewName();
        var first = Analyzer(name, "first", "ZQX0006");
        var second = Analyzer(name, "second", "ZQX0007");

        var loadedFirst = AnalyzerLoader.For([first]).LoadFromPath(first);
        var loadedSecond = AnalyzerLoader.For([second]).LoadFromPath(second);

        Assert.NotSame(loadedFirst, loadedSecond);
    }

    [Fact]
    public void ThreeContentsOfOneNameInOneBuildLoadInThreeContexts()
    {
        var name = NewName();
        var paths = new[] { Analyzer(name, "a", "ZQX0008"), Analyzer(name, "b", "ZQX0009"), Analyzer(name, "c", "ZQX0010") };

        var loader = AnalyzerLoader.For(paths);

        Assert.Equal(["ZQX0008", "ZQX0009", "ZQX0010"], paths.Select(path => Ids(loader.LoadFromPath(path)).Single()));
    }

    [Fact]
    public void APathTheBuildDidNotAskForIsRefusedByTheLoaderAndNotLoadedBlindly()
    {
        var asked = Analyzer(NewName(), "asked", "ZQX0011");
        var other = Analyzer(NewName(), "other", "ZQX0012");

        var loader = AnalyzerLoader.For([asked]);

        _ = Assert.Throws<InvalidOperationException>(() => loader.LoadFromPath(other));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string NewName() => $"Zqx{Guid.NewGuid():N}";

    private static string[] Ids(Assembly assembly) =>
        [.. assembly.GetTypes()
            .Where(type => typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract)
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!)
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .Select(descriptor => descriptor.Id)];

    private static GeneratorInput Input() =>
        new(CSharpCompilation.Create("zqx"), [], AnalyzerConfigSet.Create(ImmutableArray<AnalyzerConfig>.Empty), CSharpParseOptions.Default);

    /// <summary>The file again, byte for byte and under the same name, in another folder.</summary>
    private string Copy(string path, string folder)
    {
        var directory = Path.Combine(root, folder);
        _ = Directory.CreateDirectory(directory);
        var copy = Path.Combine(directory, Path.GetFileName(path));
        File.Copy(path, copy);
        return copy;
    }

    /// <summary>Writes an analyzer assembly of the given file name in a folder of its own, reporting one diagnostic of the given id, so that two with one name differ by content and by path.</summary>
    private string Analyzer(string name, string folder, string id)
    {
        var source = $$"""
            using System.Collections.Immutable;
            using Microsoft.CodeAnalysis;
            using Microsoft.CodeAnalysis.Diagnostics;

            [DiagnosticAnalyzer(LanguageNames.CSharp)]
            public sealed class ZqxAnalyzer : DiagnosticAnalyzer
            {
                private static readonly DiagnosticDescriptor Rule = new("{{id}}", "t", "m", "c", DiagnosticSeverity.Warning, true);

                public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

                public override void Initialize(AnalysisContext context)
                {
                }
            }
            """;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "System.Collections.Immutable.dll" or "netstandard.dll")
            .Append(typeof(DiagnosticAnalyzer).Assembly.Location)
            .Append(typeof(ImmutableArray).Assembly.Location)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var directory = Path.Combine(root, folder);
        _ = Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".dll");
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return path;
    }
}
