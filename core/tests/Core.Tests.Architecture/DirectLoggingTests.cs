using Core.Tests.Shared;

namespace Core.Tests.Architecture;

/// <summary>
/// Issue 34 CA-3: a value interpolated into a direct logger call fails the gate, including the
/// <c>ILogger.Log&lt;TState&gt;</c> route that <c>BannedSymbols.txt</c> cannot close (see <see cref="LoggingSourceRules"/>).
/// </summary>
public sealed class DirectLoggingTests
{
    [Fact]
    public void NoSourceFileCallsTheLoggerDirectly()
    {
        var source = new DirectoryInfo(Path.Combine(RepositoryRoot.Find().FullName, "core", "src"));

        var violations = LoggingSourceRules.Violations(source);

        Assert.Empty(violations);
    }

    [Fact]
    public void RuleFindsEveryDirectCallAndInterpolationInLoggingFiles()
    {
        var fixture = new DirectoryInfo(
            Path.Combine(RepositoryRoot.Find().FullName, "core", "tests", "Fixtures", "DirectLogging"));

        var violations = LoggingSourceRules.Violations(fixture);

        Assert.Equal(
            [
                "Module/Logging/LineBuilder.cs:6: interpolated string in a logging file; log text comes only from [LoggerMessage] templates",
                "Module/Worker.cs:10: " + CallTo("Log"),
                "Module/Worker.cs:11: " + CallTo("Log"),
                "Module/Worker.cs:12: " + CallTo("LogInformation"),
                "Module/Worker.cs:13: " + CallTo("LogWarning"),
                "Module/Worker.cs:14: " + CallTo("BeginScope"),
            ],
            violations);
    }

    [Fact]
    public void BuildOutputIsNotScannedButEverythingElseIs()
    {
        var root = Directory.CreateTempSubdirectory("cdg-logging-scan-");
        try
        {
            const string Leak = "logger.LogInformation(text);\n";
            foreach (var folder in new[] { "obj", "bin", "src" })
            {
                File.WriteAllText(Path.Combine(root.CreateSubdirectory(folder).FullName, "Leak.cs"), Leak);
            }

            var violations = LoggingSourceRules.Violations(root);

            Assert.Equal(["src/Leak.cs:1: " + CallTo("LogInformation")], violations);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static string CallTo(string name) => $"direct call to the logger ({name}); write a [LoggerMessage] method";
}
