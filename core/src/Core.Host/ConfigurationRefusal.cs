using Core.Platform.Configuration;
using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Tells the operator why the process did not start, one problem per line.</summary>
internal static class ConfigurationRefusal
{
    public static ExitCode Report(TextWriter output, IReadOnlyList<ConfigurationProblem> problems)
    {
        output.WriteLine("O processo não iniciou: a configuração foi recusada.");
        foreach (var problem in problems)
        {
            output.WriteLine($"  - {problem.Describe()}");
        }

        return ExitCode.ConfigurationRefused;
    }
}
