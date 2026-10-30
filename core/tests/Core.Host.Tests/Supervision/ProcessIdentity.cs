using System.ComponentModel;
using System.Diagnostics;

namespace Core.Host.Tests.Supervision;

/// <summary>
/// A process as it was read: its number and the time it started. The system hands a number to another process once the
/// first ends, so a number alone can name a stranger; the pair names the process that was read, or nothing.
/// </summary>
internal readonly record struct ProcessIdentity(int Id, DateTime Started)
{
    // On Linux the runtime derives the start time from the boot time, which it reads again at every call, so two
    // readings of one process can differ by a few milliseconds. A process that reuses the number starts later by far more.
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(1);

    /// <summary>The identity of the process with that number now, or null when none runs or its start time cannot be read.</summary>
    public static ProcessIdentity? Of(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return new ProcessIdentity(id, process.StartTime);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return null;
        }
    }

    /// <summary>The identity of a process the test knows is running; one that is not fails the test that asked.</summary>
    public static ProcessIdentity Required(int id) =>
        Of(id) ?? throw new InvalidOperationException($"Process {id} is not running, so its start time cannot be read.");

    /// <summary>The process with this number when it is the one that was read; null when there is none or the number now belongs to another.</summary>
    public Process? Open()
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(Id);
            if ((process.StartTime - Started).Duration() <= Tolerance)
            {
                return process;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            // It ended, or the number is not readable: there is no process to act on.
        }

        process?.Dispose();
        return null;
    }

    /// <summary>Ends the process that was read; whether it did. A number now held by another process, or by none, is left alone.</summary>
    public bool Kill()
    {
        using var process = Open();
        if (process is null)
        {
            return false;
        }

        try
        {
            process.Kill();
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            // It ended between the check and the kill, which is what the caller wanted.
            return false;
        }
    }
}
