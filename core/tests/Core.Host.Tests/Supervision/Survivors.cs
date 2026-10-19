using System.Diagnostics;

namespace Core.Host.Tests.Supervision;

/// <summary>Processes a test expects to end by themselves; on dispose, the ones still running are killed by their number, so a failure never leaves one behind.</summary>
internal sealed class Survivors : IDisposable
{
    private readonly IReadOnlyList<int> ids;

    public Survivors(IReadOnlyList<int> ids)
    {
        this.ids = ids;
    }

    public void Dispose()
    {
        foreach (var id in ids.Where(ProcessTree.IsRunning))
        {
            Kill(id);
        }
    }

    private static void Kill(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            process.Kill();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            // It ended between the check and the kill, which is what the test wanted.
        }
    }
}
