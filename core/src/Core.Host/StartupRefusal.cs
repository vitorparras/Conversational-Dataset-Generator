using Core.Platform.Startup;

namespace Core.Host;

/// <summary>Tells the operator which check failed and where the missing item should come from.</summary>
internal static class StartupRefusal
{
    internal static ExitCode Report(TextWriter output, StartupReport startup)
    {
        output.WriteLine("O processo não iniciou: uma conferência de inicialização falhou.");
        if (startup.FirstFailure is { } failure)
        {
            output.WriteLine($"  - {failure.Name}: {failure.Message} (origem esperada: {failure.ExpectedSource})");
        }

        return ExitCode.StartupCheckFailed;
    }
}
