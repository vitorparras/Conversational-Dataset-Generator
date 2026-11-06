namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// The one exception the build rules make for the screens of the host (decision 0052): the Razor generator reads the components of <c>core/src/Core.Host/Screens/</c>
/// and writes one file for each, and that file disables the two compiler warnings the generator always disables. Nothing else is excused: no other folder,
/// no other generator, no analyzer, and no file written by hand.
/// </summary>
internal static class RazorScreens
{
    private const string Generator = "Microsoft.CodeAnalysis.Razor.Compiler/Microsoft.NET.Sdk.Razor.SourceGenerators.RazorSourceGenerator/Screens/";
    private const string GeneratedSuffix = "_razor.g.cs";
    private const string ComponentExtension = ".razor";

    /// <summary>The compiler warnings the generator disables in every file it writes: a public member without documentation and an async method without await.</summary>
    private static readonly string[] GeneratorWarnings = ["1591", "1998"];

    private static string Folder => Path.Combine(SourceTree.CoreRoot, "src", "Core.Host", "Screens");

    /// <summary>A component of the screens, which the compiler is given so that the Razor generator reads it.</summary>
    public static bool IsComponent(string path) =>
        string.Equals(Path.GetExtension(path), ComponentExtension, StringComparison.Ordinal) && SamePaths.Are(Path.GetDirectoryName(path), Folder);

    /// <summary>A file the Razor generator wrote for a component of the screens, directly under them, whichever separator the system gives the path.</summary>
    public static bool IsGenerated(SourceFile file)
    {
        var path = file.Path.Replace('\\', '/');
        return path.StartsWith(Generator, StringComparison.Ordinal)
            && path.EndsWith(GeneratedSuffix, StringComparison.Ordinal)
            && !path[Generator.Length..].Contains('/', StringComparison.Ordinal);
    }

    /// <summary>A disable that names only the warnings the generator always disables, in a file it wrote for the screens.</summary>
    public static bool IsGeneratorPragma(SourceFile file, IReadOnlyCollection<string> codes) =>
        IsGenerated(file) && codes.Count > 0 && codes.All(code => Array.IndexOf(GeneratorWarnings, code) >= 0);
}
