using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Tells the operator which check failed and where the missing item should come from.</summary>
internal static class StartupRefusal
{
    public static ExitCode Report(TextWriter output, StartupReport report)
    {
        output.WriteLine("O processo não iniciou: uma conferência de inicialização falhou.");
        foreach (var failure in report.Results.Where(result => result.Status == CheckStatus.Failed))
        {
            output.WriteLine($"  - {failure.Name}: {failure.Message} (origem esperada: {failure.ExpectedSource})");
        }

        return ExitCode.StartupCheckFailed;
    }
}
