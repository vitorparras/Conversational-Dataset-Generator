using System.ComponentModel;
using System.Diagnostics;

namespace Core.Host.Tests;

/// <summary>The Python interpreter of the language service environment, asked of uv once for the whole test run.</summary>
internal static class LanguageServiceInterpreter
{
    private const string ServiceFolder = "language-service";
    private static readonly Lazy<string> Resolved = new(() => Resolve("sys.executable"));
    private static readonly Lazy<string> Base = new(() => Resolve("sys._base_executable"));

    public static string Path => Resolved.Value;

    /// <summary>The interpreter the environment is built on, which runs alone with no launcher in front of it, as the one in the service folder does.</summary>
    public static string Standalone => Base.Value;

    /// <summary>The interpreter, or null when uv is missing or cannot resolve the environment, for a rehearsal that skips without it.</summary>
    public static string? Available
    {
        get
        {
            try
            {
                return Resolved.Value;
            }
            catch (Exception missing) when (missing is InvalidOperationException or Win32Exception)
            {
                return null;
            }
        }
    }

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

    private static string Resolve(string expression)
    {
        var info = new ProcessStartInfo("uv") { RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var argument in (string[])["run", "--frozen", "--project", ProjectFolder(), "python", "-c", $"import sys; print({expression})"])
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
