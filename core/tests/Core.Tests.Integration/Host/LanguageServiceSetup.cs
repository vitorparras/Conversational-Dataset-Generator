using System.Diagnostics;
using Core.Tests.Shared;

namespace Core.Tests.Integration.Host;

/// <summary>
/// The command that starts a language service, as the configuration declares it. Two services are
/// available to the tests: the real Python one, through <c>uv</c>, and a small stand-in that speaks
/// only what the supervision uses.
/// </summary>
internal sealed record LanguageServiceSetup(string Command, string Arguments)
{
    /// <summary>Why a test that needs the real service is skipped when <c>uv</c> is missing.</summary>
    public const string UvMissing =
        "SKIPPED: 'uv' is not on the PATH, so the real language service cannot start. Install uv " +
        "(see 'Serviço de linguagem' in README.md) to run this test.";

    /// <summary>The stand-in service in one of its modes: serve, hang or exit.</summary>
    public static LanguageServiceSetup Fake(string mode = "serve") =>
        new(
            ChildProcess.Dotnet,
            string.Join(
                ';',
                Path.Combine(
                    RepositoryRoot.Find().FullName, "core", "tests", "Fixtures", "FakeLanguageService",
                    "bin", RepositoryRoot.Configuration, "net10.0", "FakeLanguageService.dll"),
                "--mode",
                mode));

    /// <summary>The real service, started with <c>uv run</c> from the folder of the project.</summary>
    public static LanguageServiceSetup Real() =>
        new(
            "uv",
            string.Join(
                ';',
                "run",
                "--frozen",
                "--directory",
                Path.Combine(RepositoryRoot.Find().FullName, "language-service"),
                "python",
                "-m",
                "language_service"));

    /// <summary>Whether <c>uv</c> can be started from here.</summary>
    public static bool UvAvailable()
    {
        try
        {
            using var uv = Process.Start(new ProcessStartInfo("uv", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            uv!.WaitForExit();
            return uv.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>The same setup as the environment variables the host reads.</summary>
    public IReadOnlyDictionary<string, string?> AsEnvironment(int? timeoutSeconds = null)
    {
        var variables = new Dictionary<string, string?>
        {
            ["CDG__LanguageService__Command"] = Command,
            ["CDG__LanguageService__Arguments"] = Arguments,
        };
        if (timeoutSeconds is { } seconds)
        {
            variables["CDG__LanguageService__StartupTimeoutSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return variables;
    }
}
