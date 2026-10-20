using Microsoft.CodeAnalysis;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Finds, in what a build at normal verbosity printed, the compiler command line of one project.</summary>
internal static class CompilerCommandLine
{
    private const string FirstArgument = "/noconfig";
    private const string OutputArgument = "/out:";

    /// <summary>The arguments of the compiler run whose output is the project's assembly, from <c>/noconfig</c> on.</summary>
    public static IReadOnlyList<string> Of(string buildOutput, string projectPath)
    {
        var assembly = Path.GetFileNameWithoutExtension(projectPath) + ".dll";
        return buildOutput.Split('\n')
            .Select(line => CommandLineParser.SplitCommandLineIntoArguments(line.Trim(), removeHashComments: false).ToList())
            .Where(arguments => arguments.Contains(FirstArgument, StringComparer.Ordinal))
            .Select(arguments => arguments[arguments.IndexOf(FirstArgument)..])
            .LastOrDefault(arguments => arguments.Exists(argument => IsOutput(argument, assembly)))
            ?? throw new InvalidOperationException($"The compiler command line of {assembly} is not in the build output.");
    }

    /// <summary>The folder the compiler writes the assembly to, which is the root of the project's intermediate output.</summary>
    public static string IntermediateDirectory(IEnumerable<string> arguments, string projectPath)
    {
        var output = arguments.First(argument => argument.StartsWith(OutputArgument, StringComparison.Ordinal))[OutputArgument.Length..].Trim('"');
        return Path.GetDirectoryName(Path.GetFullPath(output, Path.GetDirectoryName(projectPath)!))!;
    }

    private static bool IsOutput(string argument, string assembly) =>
        argument.StartsWith(OutputArgument, StringComparison.Ordinal)
        && Path.GetFileName(argument[OutputArgument.Length..].Trim('"')).Equals(assembly, StringComparison.OrdinalIgnoreCase);
}
