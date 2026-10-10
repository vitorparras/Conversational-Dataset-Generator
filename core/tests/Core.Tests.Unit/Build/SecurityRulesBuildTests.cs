using Core.Tests.Shared;

namespace Core.Tests.Unit.Build;

/// <summary>
/// The security rules of the analyzer are errors on their own, not only through
/// TreatWarningsAsErrors (core/build/SecurityAnalyzers.targets). The fixture under
/// core/tests/Fixtures is outside the solution, keeps warnings as warnings, and is compiled here.
/// </summary>
public sealed class SecurityRulesBuildTests
{
    [Fact]
    public async Task BrokenCryptographyFailsTheBuildWithTheSecurityRuleAsAnError()
    {
        var project = Path.Combine(
            RepositoryRoot.Find().FullName, "core", "tests", "Fixtures", "BrokenCryptography", "BrokenCryptography.csproj");

        var (exitCode, output) = await ChildProcess.RunAsync(
            ChildProcess.Dotnet,
            ["build", project, "-nologo", "-nodeReuse:false", "-p:UseSharedCompilation=false"],
            Path.GetDirectoryName(project)!);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("Digest.cs(9,45): error CA5351", output, StringComparison.Ordinal);
    }
}
