using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Reads the rules out of the restored SonarAnalyzer package, so a version bump brings its new rules into view.</summary>
internal static partial class SonarRuleCatalog
{
    private const string PathKey = "SonarAnalyzerPath";

    public static string AnalyzerPath { get; } = typeof(SonarRuleCatalog).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(attribute => attribute.Key == PathKey).Value!;

    public static IReadOnlyList<SonarRule> Load(string analyzerPath) =>
        [.. Analyzers(AssemblyLoadContext.Default.LoadFromAssemblyPath(analyzerPath))
            .SelectMany(analyzer => analyzer.SupportedDiagnostics)
            .Where(descriptor => RuleId().IsMatch(descriptor.Id))
            .DistinctBy(descriptor => descriptor.Id)
            .OrderBy(descriptor => descriptor.Id, StringComparer.Ordinal)
            .Select(descriptor => new SonarRule(descriptor.Id, descriptor.DefaultSeverity, descriptor.IsEnabledByDefault))];

    private static IEnumerable<DiagnosticAnalyzer> Analyzers(Assembly assembly) =>
        Types(assembly)
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
            .Select(type => (DiagnosticAnalyzer)Activator.CreateInstance(type)!);

    /// <summary>The analyzer assembly also holds code fixes whose host assembly is not loaded; only the analyzers matter here.</summary>
    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException partial)
        {
            return partial.Types.OfType<Type>();
        }
    }

    [GeneratedRegex(@"^S\d+$")]
    private static partial Regex RuleId();
}
