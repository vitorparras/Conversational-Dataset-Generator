using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Reads the arguments of a <c>csc</c> command line the way the compiler does, response files included, and runs its generators on the sources it names.</summary>
internal static class CompilerArguments
{
    public static CompilerInvocation Read(CompilerBuildOutput build)
    {
        var projectDirectory = Path.GetDirectoryName(build.ProjectPath)!;
        var parsed = CSharpCommandLineParser.Default.Parse(build.Arguments, projectDirectory, AppContext.BaseDirectory);
        var errors = parsed.Errors.Where(error => error.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"The compiler arguments of {Path.GetFileName(build.ProjectPath)} do not parse: {string.Join("; ", errors)}");
        }

        var compiled = parsed.SourceFiles.Select(file => file.Path).ToList();
        var sources = compiled.Where(path => !IsUnder(path, build.ArtifactsDirectory)).ToList();
        var analyzers = parsed.AnalyzerReferences.Select(reference => Path.GetFullPath(reference.FilePath, projectDirectory)).ToList();
        var configs = AnalyzerConfigSet.Create(parsed.AnalyzerConfigPaths.Select(path => AnalyzerConfig.Parse(File.ReadAllText(path), path)).ToImmutableArray());
        var additional = parsed.AdditionalFiles.Select(file => file.Path).ToList();
        var findings = AnalyzerInspector.Inspect(analyzers, new GeneratorInput(CompilationOf(parsed, compiled), additional, configs, parsed.ParseOptions));
        return new CompilerInvocation
        {
            ProjectPath = build.ProjectPath,
            AssemblyName = parsed.CompilationName ?? Path.GetFileNameWithoutExtension(build.ProjectPath),
            ProjectReferences = [.. parsed.MetadataReferences.Select(reference => reference.Reference).Where(path => IsUnder(path, build.ArtifactsDirectory)).Select(NameOf)],
            Sources = sources,
            SourceFiles = [.. sources.Select(SourceFile.Read)],
            BuildAddedFiles = [.. compiled.Except(sources).Select(SourceFile.Read)],
            GeneratorFiles = [.. findings.GeneratedSources],
            IntermediateDirectory = build.IntermediateDirectory,
            AnalyzerConfigPaths = [.. parsed.AnalyzerConfigPaths],
            Analyzers = analyzers,
            Suppressors = findings.Suppressors,
            AnalyzerFailures = findings.Failures,
            AdditionalFiles = additional,
            DiagnosticsByAnalyzer = findings.DiagnosticsByAnalyzer,
            AnalyzerPackages = PackageIdentity.InCache(analyzers, build.AnalyzerRoots.Skip(1)),
            PreprocessorSymbols = [.. parsed.ParseOptions.PreprocessorSymbolNames],
            AnalyzerRoots = build.AnalyzerRoots,
            SpecificDiagnosticOptions = parsed.CompilationOptions.SpecificDiagnosticOptions,
            GeneralDiagnosticOption = parsed.CompilationOptions.GeneralDiagnosticOption,
            SkipAnalyzers = parsed.SkipAnalyzers,
            RuleSetPath = parsed.RuleSetPath,
            ConfigSet = configs,
        };
    }

    private static CSharpCompilation CompilationOf(CSharpCommandLineArguments parsed, IEnumerable<string> sources) =>
        CSharpCompilation.Create(
            "Bound",
            sources.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parsed.ParseOptions, path)),
            parsed.MetadataReferences.Select(reference => MetadataReference.CreateFromFile(reference.Reference, reference.Properties)),
            parsed.CompilationOptions);

    /// <summary>The name an assembly carries in its own metadata, whatever its file is called.</summary>
    private static string NameOf(string path) => System.Reflection.AssemblyName.GetAssemblyName(path).Name!;

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
