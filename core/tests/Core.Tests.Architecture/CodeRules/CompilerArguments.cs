using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Sorts the arguments of a <c>csc</c> command line into what the rules ask about.</summary>
internal static partial class CompilerArguments
{
    public static CompilerInvocation Read(string projectPath, IReadOnlyList<string> arguments, string generatedDirectory)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var sources = arguments.Where(IsSource).Select(source => Path.GetFullPath(source, projectDirectory)).Where(source => !IsUnder(source, generatedDirectory)).ToList();
        var configs = PathsOf(arguments, "/analyzerconfig:", projectDirectory).ToList();
        return new CompilerInvocation
        {
            ProjectPath = projectPath,
            Sources = sources,
            SourceFiles = [.. sources.Select(SourceFile.Read)],
            AnalyzerConfigs = configs,
            Analyzers = [.. PathsOf(arguments, "/analyzer:", projectDirectory)],
            AdditionalFiles = [.. PathsOf(arguments, "/additionalfile:", projectDirectory)],
            NoWarn = [.. ValuesOf(arguments, "/nowarn:").SelectMany(list => list.Split(','))],
            WarningsAsErrors = arguments.Any(argument => argument is "/warnaserror" or "/warnaserror+"),
            ConfigSet = AnalyzerConfigSet.Create(configs.Select(path => AnalyzerConfig.Parse(File.ReadAllText(path), path)).ToImmutableArray()),
        };
    }

    private static IEnumerable<string> PathsOf(IEnumerable<string> arguments, string prefix, string projectDirectory) =>
        ValuesOf(arguments, prefix).Select(path => Path.GetFullPath(path, projectDirectory));

    private static IEnumerable<string> ValuesOf(IEnumerable<string> arguments, string prefix) =>
        arguments.Where(argument => argument.StartsWith(prefix, StringComparison.Ordinal)).Select(argument => argument[prefix.Length..].Trim('"'));

    /// <summary>A source file ends in <c>.cs</c> and is not a switch such as <c>/embed:</c>, which can end that way too.</summary>
    private static bool IsSource(string argument) =>
        argument.EndsWith(".cs", StringComparison.Ordinal) && !SwitchWithValue().IsMatch(argument);

    private static bool IsUnder(string path, string directory) =>
        path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("^/[A-Za-z]+:")]
    private static partial Regex SwitchWithValue();
}
