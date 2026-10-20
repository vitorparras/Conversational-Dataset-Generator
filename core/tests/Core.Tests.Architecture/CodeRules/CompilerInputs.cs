namespace Core.Tests.Architecture.CodeRules;

/// <summary>
/// What the compiler is given that decides what it reports, by kind, with each path written from the place it lives in (the
/// repository, the intermediate folder, the package cache or the SDK), so that the same tree built in two folders compares equal.
/// </summary>
internal sealed record CompilerInputs(IReadOnlyDictionary<string, IReadOnlySet<string>> Kinds)
{
    public static CompilerInputs Of(CompilerInvocation invocation, string repositoryRoot)
    {
        string Place(string path) => PlaceOf(path, invocation.IntermediateDirectory, repositoryRoot);

        return new CompilerInputs(new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["fonte"] = Set(invocation.Sources.Concat(invocation.BuildAddedFiles.Select(file => file.Path)).Select(Place)),
            ["símbolo de pré-processador"] = Set(invocation.PreprocessorSymbols),
            ["analisador"] = Set(invocation.Analyzers.Select(Place)),
            ["configuração de analisador"] = Set(invocation.AnalyzerConfigPaths.Select(Place)),
            ["arquivo adicional"] = Set(invocation.AdditionalFiles.Select(Place)),
            ["opção de diagnóstico"] = Set(DiagnosticOptions(invocation, Place)),
        });
    }

    private static HashSet<string> Set(IEnumerable<string> items) => new(items, StringComparer.Ordinal);

    private static IEnumerable<string> DiagnosticOptions(CompilerInvocation invocation, Func<string, string> place) =>
        invocation.SpecificDiagnosticOptions.Select(option => $"{option.Key}={option.Value}")
            .Append($"todos os avisos={invocation.GeneralDiagnosticOption}")
            .Append($"analisadores desligados={invocation.SkipAnalyzers}")
            .Append($"ruleset={(invocation.RuleSetPath is null ? "nenhum" : place(invocation.RuleSetPath))}");

    private static string PlaceOf(string path, string intermediate, string repositoryRoot)
    {
        var full = Path.GetFullPath(path);
        var roots = new[] { ("obj", intermediate), ("repositório", repositoryRoot), ("pacotes", ToolLocations.GlobalPackages), ("dotnet", ToolLocations.DotnetRoot) };
        var (name, root) = Array.Find(roots, entry => IsUnder(full, entry.Item2));
        return name is null ? full : $"{name}:{Path.GetRelativePath(root, full).Replace(Path.DirectorySeparatorChar, '/')}";
    }

    private static bool IsUnder(string path, string root) =>
        !string.IsNullOrEmpty(root)
        && path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
