using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>What a production build produces for itself, and the suppressors it carries, are only what the SDK and the generators of the core are known to bring.</summary>
internal static class BuildOutputRules
{
    private const string TargetFrameworkPrefix = ".NETCoreApp,Version=v";
    private const string TargetFrameworkSuffix = ".AssemblyAttributes.cs";

    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>The generators allowed to emit a file, each with the one file it emits: the analyzer skips generated code, so no other output is accepted.</summary>
    private static readonly (string Generator, string Hint)[] KnownGeneratorOutputs =
    [
        ("Microsoft.AspNetCore.SourceGenerators.PublicProgramSourceGenerator", "PublicTopLevelProgram.Generated.g.cs"),
        ("Microsoft.Gen.Logging.LoggingGenerator", "Logging.g.cs"),
    ];

    /// <summary>
    /// The suppressors the SDK itself carries, each with the form in which it acts. None acts on a rule of the core:
    /// the first only on members of a type marked as a custom marshaller of the interop generator (CA1822), which the core has none of,
    /// the second only on a property that is a parameter of a Razor component (CS8618), which the core has none of.
    /// </summary>
    private static readonly (string Type, string Id)[] SdkSuppressors =
    [
        ("Microsoft.Interop.Analyzers.ShapeBreakingDiagnosticSuppressor", "CA1822"),
        ("Microsoft.CodeAnalysis.Razor.Compiler.Analyzers.ComponentParameterNullableWarningSuppressor", "CS8618"),
    ];

    /// <summary>A file compiled from the intermediate folder is one the SDK writes there, by name and by form; a file a generator emits is one a named generator emits.</summary>
    public static IEnumerable<Violation> CheckGeneratedFiles(CompilerInvocation invocation) =>
        invocation.BuildAddedFiles
            .Where(file => !IsSdkFile(invocation, file))
            .Select(file => new Violation(file.Path, 1, "a source compiled from the build output is not a file the SDK writes at the root of the intermediate folder"))
            .Concat(invocation.GeneratorFiles
                .Where(file => !IsKnownGeneratorOutput(file))
                .Select(file => new Violation(file.Path, 1, "a generator emitted a file that is not the known output of a named generator")));

    public static IEnumerable<Violation> CheckSuppressors(CompilerInvocation invocation) =>
        invocation.Suppressors
            .Where(suppressor => !IsSdkSuppressor(suppressor))
            .Select(suppressor => new Violation(
                invocation.ProjectPath, 1, $"{suppressor.TypeName} in {Path.GetFileName(suppressor.AnalyzerPath)} is able to suppress {string.Join(", ", suppressor.SuppressedIds)}"));

    private static bool IsSdkSuppressor(AnalyzerSuppressor suppressor) =>
        suppressor.AnalyzerPath.StartsWith(ToolLocations.DotnetRoot, StringComparison.OrdinalIgnoreCase)
        && suppressor.SuppressedIds.All(id => Array.Exists(SdkSuppressors, known => known.Type == suppressor.TypeName && known.Id == id));

    private static bool IsKnownGeneratorOutput(SourceFile file)
    {
        var parts = file.Path.Split(Separators);
        return parts.Length == 3 && Array.Exists(KnownGeneratorOutputs, known => parts[1] == known.Generator && parts[2] == known.Hint);
    }

    private static bool IsSdkFile(CompilerInvocation invocation, SourceFile file)
    {
        var project = Path.GetFileNameWithoutExtension(invocation.ProjectPath);
        var inRoot = SamePaths.Are(Path.GetDirectoryName(file.Path), invocation.IntermediateDirectory);
        var unit = (CompilationUnitSyntax)file.Root;
        return inRoot && unit.Members.Count == 0 && unit.Externs.Count == 0 && file.FileName switch
        {
            var name when name == project + ".GlobalUsings.g.cs" => IsOnlyGlobalUsings(unit),
            var name when name == project + ".AssemblyInfo.cs" => IsOnlyAssemblyAttributes(unit),
            var name when name.StartsWith(TargetFrameworkPrefix, StringComparison.Ordinal) && name.EndsWith(TargetFrameworkSuffix, StringComparison.Ordinal) => IsOnlyAssemblyAttributes(unit),
            _ => false,
        };
    }

    private static bool IsOnlyGlobalUsings(CompilationUnitSyntax unit) =>
        unit.AttributeLists.Count == 0 && unit.Usings.All(directive => directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword));

    private static bool IsOnlyAssemblyAttributes(CompilationUnitSyntax unit) =>
        unit.AttributeLists.All(list => list.Target?.Identifier.IsKind(SyntaxKind.AssemblyKeyword) == true);
}
