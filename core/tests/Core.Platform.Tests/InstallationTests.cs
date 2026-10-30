using System.Text.Json;
using Core.Platform.Versions;

namespace Core.Platform.Tests;

/// <summary>A demonstration says so in its name, a real installation carries no mark, and the report of versions shows the one it was given.</summary>
public sealed class InstallationTests
{
    private static readonly BuildInfo Build = new(
        VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Clean);

    [Fact]
    public void DemonstrationCarriesTheMarkInItsName()
    {
        var installation = new Installation(InstallationKind.Demonstration);

        Assert.Equal("Conversational Dataset Generator (DEMONSTRAÇÃO)", installation.Name);
    }

    [Fact]
    public void RealInstallationCarriesNoMarkInItsName()
    {
        var installation = new Installation(InstallationKind.Real);

        Assert.Equal("Conversational Dataset Generator", installation.Name);
    }

    [Fact]
    public void ReportHasNoInstallationUntilItIsGivenOne()
    {
        var report = new VersionReport(Build);

        Assert.Null(report.Installation);
    }

    [Fact]
    public void ReportWithTheInstallationGivenCarriesItAndKeepsTheBuild()
    {
        var installation = new Installation(InstallationKind.Demonstration);

        var report = new VersionReport(Build).WithInstallation(installation);

        Assert.Equal((installation, Build), (report.Installation, new BuildInfo(report.CoreVersion, report.Commit, report.WorkingTree)));
    }

    [Fact]
    public void ReportWithTheInstallationGivenLeavesTheOriginalAsItWas()
    {
        var original = new VersionReport(Build);

        _ = original.WithInstallation(new Installation(InstallationKind.Real));

        Assert.Null(original.Installation);
    }

    [Fact]
    public void ReportWithoutAnInstallationGivenIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new VersionReport(Build).WithInstallation(null!));

    [Fact]
    public void ReportWritesTheNameOfTheDemonstrationInItsJson()
    {
        using var json = JsonSerializer.SerializeToDocument(new VersionReport(Build).WithInstallation(new Installation(InstallationKind.Demonstration)));

        Assert.Equal("Conversational Dataset Generator (DEMONSTRAÇÃO)", json.RootElement.GetProperty("Installation").GetProperty("Name").GetString());
    }
}
