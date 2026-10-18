using System.Diagnostics;

namespace Core.Host.Tests;

/// <summary>The Python interpreter of the language service environment, asked of uv once for the whole test run.</summary>
internal static class LanguageServiceInterpreter
{
    private const string ServiceFolder = "language-service";
    private static readonly Lazy<string> Resolved = new(Resolve);

    public static string Path => Resolved.Value;

    /// <summary>The language service project folder, found above the test binaries.</summary>
    public static string ProjectFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory.FullName, ServiceFolder)))
        {
            directory = directory.Parent;
        }

        return System.IO.Path.Combine(
            (directory ?? throw new InvalidOperationException("The repository root was not found above the test binaries.")).FullName,
            ServiceFolder);
    }

    private static string Resolve()
    {
        var info = new ProcessStartInfo("uv") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in (string[])["run", "--frozen", "--project", ProjectFolder(), "python", "-c", "import sys; print(sys.executable)"])
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("uv did not start.");
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && output.Length > 0
            ? output
            : throw new InvalidOperationException("uv could not resolve the language service environment; run 'uv sync --frozen' in language-service.");
    }
}
