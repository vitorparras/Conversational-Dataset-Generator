using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Core.Tests.Integration.Host;

/// <summary>
/// Asks a process to shut down the way an operator does: SIGTERM on Linux and macOS, a console
/// break on Windows. Never a kill: the point is to see the process close its log and choose its
/// exit code.
/// </summary>
internal static partial class ShutdownSignal
{
    // Ctrl+Break, not Ctrl+C: a process can inherit "ignore Ctrl+C" from whoever started the test
    // run, and Ctrl+Break cannot be ignored that way. .NET stops the host on either.
    private const uint CtrlBreakEvent = 1;

    public static async Task SendAsync(HostProcess host)
    {
        if (OperatingSystem.IsWindows())
        {
            SendConsoleBreak(host.Id);
            return;
        }

        using var kill = Process.Start("kill", ["-s", "TERM", host.Id.ToString(CultureInfo.InvariantCulture)]);
        await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
    }

    // A console event reaches every process attached to a console. The test process joins the
    // console of the host, swallows the event itself, sends it, and leaves. Integration tests never
    // run in parallel, so no other host shares that console.
    private static unsafe void SendConsoleBreak(int processId)
    {
        _ = FreeConsole();
        if (!AttachConsole((uint)processId))
        {
            throw new InvalidOperationException(
                "could not attach to the console of the host; error " + Marshal.GetLastPInvokeError());
        }

        _ = SetConsoleCtrlHandler(&Swallow, add: true);
        try
        {
            if (!GenerateConsoleCtrlEvent(CtrlBreakEvent, 0))
            {
                throw new InvalidOperationException(
                    "could not send the console break; error " + Marshal.GetLastPInvokeError());
            }

            // The event is delivered on another thread; leaving the console too early can lose it.
            Thread.Sleep(500);
        }
        finally
        {
            _ = FreeConsole();
            _ = SetConsoleCtrlHandler(&Swallow, add: false);
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int Swallow(uint ctrlType) => 1;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SetConsoleCtrlHandler(
        delegate* unmanaged[Stdcall]<uint, int> handler, [MarshalAs(UnmanagedType.Bool)] bool add);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GenerateConsoleCtrlEvent(uint ctrlEvent, uint processGroupId);
}
