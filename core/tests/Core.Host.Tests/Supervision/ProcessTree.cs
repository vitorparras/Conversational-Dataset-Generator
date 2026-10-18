using System.Diagnostics;
using System.Globalization;

namespace Core.Host.Tests.Supervision;

/// <summary>The processes the operating system lists, as another account on the machine would see them.</summary>
internal static class ProcessTree
{
    private const string WindowsQuery =
        "Get-CimInstance Win32_Process | ForEach-Object { '{0}|{1}|{2}' -f $_.ProcessId, $_.ParentProcessId, $_.CommandLine }";

    private sealed record Row(int Id, int ParentId, string CommandLine);

    /// <summary>The process and everything below it, as the system lists them right now.</summary>
    public static async Task<IReadOnlyList<ProcessLine>> TreeOfAsync(int rootId, CancellationToken cancellationToken)
    {
        var table = Parse(await ListAsync(cancellationToken));
        var members = new HashSet<int> { rootId };
        List<int> added;
        do
        {
            added = [.. table.Where(row => !members.Contains(row.Id) && members.Contains(row.ParentId)).Select(row => row.Id)];
            members.UnionWith(added);
        }
        while (added.Count > 0);

        return [.. table.Where(row => members.Contains(row.Id)).Select(row => new ProcessLine(row.Id, row.CommandLine))];
    }

    public static bool IsRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited && !IsZombie(id);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>A process that ended but whose parent has not collected it yet, which a container without an init process never does.</summary>
    private static bool IsZombie(int id)
    {
        try
        {
            var stat = File.ReadAllText($"/proc/{id}/stat");
            return stat[(stat.LastIndexOf(')') + 2)..].StartsWith('Z');
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static Task<string> ListAsync(CancellationToken cancellationToken) =>
        OperatingSystem.IsWindows()
            ? OutputOfAsync("powershell", ["-NoProfile", "-NonInteractive", "-Command", WindowsQuery], cancellationToken)
            : OutputOfAsync("ps", ["-e", "-o", "pid=,ppid=,args="], cancellationToken);

    private static List<Row> Parse(string output)
    {
        char[] separators = ['|', ' '];
        var rows = new List<Row>();
        foreach (var line in output.Split('\n').Select(text => text.Trim()).Where(text => text.Length > 0))
        {
            var parts = line.Split(separators, 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2
                && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parent))
            {
                rows.Add(new Row(id, parent, parts.Length == 3 ? parts[2] : string.Empty));
            }
        }

        return rows;
    }

    private static async Task<string> OutputOfAsync(string fileName, string[] arguments, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(fileName) { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("The process listing did not start.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }
}
