using System.Diagnostics;
using System.Globalization;

namespace Core.Tests.Integration.Supervision;

/// <summary>
/// The command lines the operating system reports for a process and everything below it: what any
/// other account on the machine could read, as opposed to what the core put in its start information.
/// </summary>
internal static class ProcessCommandLines
{
    private const string WindowsQuery =
        "Get-CimInstance Win32_Process | ForEach-Object { '{0}|{1}|{2}' -f $_.ProcessId, $_.ParentProcessId, $_.CommandLine }";

    /// <summary>The identifier and command line of <paramref name="rootId"/> and of every process descended from it.</summary>
    public static async Task<IReadOnlyList<(int Id, string CommandLine)>> OfTreeAsync(int rootId)
    {
        var table = OperatingSystem.IsWindows() ? await WindowsAsync() : await PosixAsync();
        var members = new HashSet<int> { rootId };
        bool grew;
        do
        {
            var before = members.Count;
            foreach (var (id, _, _) in table.Where(row => members.Contains(row.ParentId)))
            {
                _ = members.Add(id);
            }

            grew = members.Count > before;
        }
        while (grew);

        return [.. table.Where(row => members.Contains(row.Id)).Select(row => (row.Id, row.CommandLine))];
    }

    private static async Task<List<(int Id, int ParentId, string CommandLine)>> WindowsAsync() =>
        Parse(await RunAsync("powershell", ["-NoProfile", "-NonInteractive", "-Command", WindowsQuery]), '|');

    private static async Task<List<(int Id, int ParentId, string CommandLine)>> PosixAsync() =>
        Parse(await RunAsync("ps", ["-e", "-o", "pid=,ppid=,args="]), ' ');

    private static List<(int Id, int ParentId, string CommandLine)> Parse(string output, char separator)
    {
        var rows = new List<(int, int, string)>();
        foreach (var line in output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0))
        {
            var parts = line.Split(separator, 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent))
            {
                rows.Add((id, parent, parts.Length == 3 ? parts[2] : string.Empty));
            }
        }

        return rows;
    }

    private static async Task<string> RunAsync(string fileName, string[] arguments)
    {
        var info = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return output;
    }
}
