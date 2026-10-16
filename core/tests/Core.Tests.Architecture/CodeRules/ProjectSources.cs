using System.Collections.Concurrent;
using System.Text.Json;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The sources a project really compiles, as the build tool evaluates its <c>Compile</c> items, wherever on disk they sit.</summary>
internal static class ProjectSources
{
    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyList<SourceFile>>> Evaluated = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<SourceFile> Of(string projectPath) =>
        Evaluated.GetOrAdd(projectPath, path => new Lazy<IReadOnlyList<SourceFile>>(() => Evaluate(path))).Value;

    /// <summary>The sources of a fixture project, evaluated in a temporary copy of the fixture tree so that its relative includes resolve.</summary>
    public static IReadOnlyList<SourceFile> OfFixture(string relativeDirectory, string projectName)
    {
        using var scratch = ScratchDirectory.Create();
        var tree = scratch.CopyFixture(Path.Combine(SourceTree.FixturesDirectory, relativeDirectory), "tree");
        return Evaluate(Path.Combine(tree, projectName, projectName + ".csproj"));
    }

    private static IReadOnlyList<SourceFile> Evaluate(string projectPath)
    {
        var outcome = DotnetCommand.RunAsync(
            ["msbuild", projectPath, "-getItem:Compile", "-nologo"], Path.GetDirectoryName(projectPath)!, CancellationToken.None)
            .GetAwaiter().GetResult();
        using var document = JsonDocument.Parse(outcome.OutputOfSuccess($"Evaluating {Path.GetFileName(projectPath)}"));
        return [.. document.RootElement.GetProperty("Items").GetProperty("Compile").EnumerateArray()
            .Select(item => item.GetProperty("FullPath").GetString()!)
            .Order(StringComparer.Ordinal)
            .Select(SourceFile.Read)];
    }
}
