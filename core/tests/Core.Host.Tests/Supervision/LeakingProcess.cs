using System.Diagnostics;

namespace Core.Host.Tests.Supervision;

/// <summary>A process that sleeps with a marker in its command line, which is where a secret passed as an argument would be; killed on dispose.</summary>
internal sealed class LeakingProcess : IDisposable
{
    private readonly Process process;

    public LeakingProcess(string marker)
    {
        // Redirecting makes the runtime serialize the start; a plain start could inherit pipe handles other tests are creating.
        var info = new ProcessStartInfo(LanguageServiceInterpreter.Path) { RedirectStandardInput = true, UseShellExecute = false };
        foreach (var argument in (string[])["-c", "import time; time.sleep(30)", marker])
        {
            info.ArgumentList.Add(argument);
        }

        process = Process.Start(info) ?? throw new InvalidOperationException("The process did not start.");
    }

    public int Id => process.Id;

    public void Dispose()
    {
        process.Kill(entireProcessTree: true);
        process.Dispose();
    }
}
