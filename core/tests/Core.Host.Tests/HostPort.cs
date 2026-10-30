using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Core.Host.Tests;

/// <summary>
/// Loopback ports for hosts that listen, each claimed by the test process that takes it for as long as the process
/// lives. The host takes its address from its configuration and does not report the port it got, so a port cannot be
/// left to the system; the claim is what keeps two test processes of one run from being given the same one, with no
/// moment in which a port was chosen and nobody holds it. The range stays below the ones the systems hand out to
/// outgoing connections. A claim file is deleted when its handle closes, which the process does on the way out, and
/// the system does if it is killed on Windows. The claim binds test processes only: between the check that nothing
/// listens on the port and the host listening on it, a process that is not a test of this run can still take the port,
/// and the range cannot rule that out; the host then fails to start and says so.
/// </summary>
internal static class HostPort
{
    private const int First = 20000;
    private const int Last = 29999;
    private const int Attempts = 200;
    private const int SharingViolation = 32;
    private const int LockViolation = 33;
    private const int WouldBlockOnLinux = 11;

    private static readonly string ClaimsDirectory = Path.Combine(Path.GetTempPath(), "cdg-test-host-ports");
    private static readonly List<FileStream> Claims = [];
    private static readonly Lock Gate = new();

    static HostPort()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Release();
    }

    /// <summary>The port, claimed and not in use by anyone else at the time of the claim.</summary>
    public static int Claim()
    {
        _ = Directory.CreateDirectory(ClaimsDirectory);
        var refused = new List<string>();
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            var port = RandomNumberGenerator.GetInt32(First, Last + 1);
            var claim = Hold(port);
            if (claim is null)
            {
                refused.Add($"{port} (claimed by another test process)");
                continue;
            }

            if (!InUse(port))
            {
                lock (Gate)
                {
                    Claims.Add(claim);
                }

                return port;
            }

            claim.Dispose();
            refused.Add($"{port} (in use)");
        }

        throw new InvalidOperationException(
            $"No loopback port in {First}-{Last} could be claimed in {Attempts} attempts: {string.Join(", ", refused.Take(5))}, ...");
    }

    /// <summary>The claim file of the port, held open without sharing; null when another process holds it, and any other failure is the caller's.</summary>
    private static FileStream? Hold(int port)
    {
        try
        {
            return new FileStream(
                Path.Combine(ClaimsDirectory, $"{port}.claim"),
                new FileStreamOptions
                {
                    Mode = FileMode.OpenOrCreate,
                    Access = FileAccess.ReadWrite,
                    Share = FileShare.None,
                    Options = FileOptions.DeleteOnClose,
                });
        }
        catch (IOException failure) when (HeldByAnotherProcess(failure))
        {
            return null;
        }
    }

    /// <summary>How each system reports a file another handle holds without sharing it: a sharing or lock violation on Windows, EWOULDBLOCK from the lock on Linux.</summary>
    private static bool HeldByAnotherProcess(Exception failure) =>
        OperatingSystem.IsWindows() ? (failure.HResult & 0xFFFF) is SharingViolation or LockViolation : failure.HResult == WouldBlockOnLinux;

    private static void Release()
    {
        lock (Gate)
        {
            foreach (var claim in Claims)
            {
                claim.Dispose();
            }

            Claims.Clear();
        }
    }

    private static bool InUse(int port)
    {
        using var probe = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        try
        {
            probe.Bind(new IPEndPoint(IPAddress.Loopback, port));
            return false;
        }
        catch (SocketException)
        {
            return true;
        }
    }
}
