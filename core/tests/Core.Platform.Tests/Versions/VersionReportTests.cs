using System.Text.Json;
using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>The report carries the build of the core and declares absent every version the core cannot obtain yet.</summary>
public sealed class VersionReportTests
{
    private static readonly BuildInfo Build = new(
        VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Dirty);

    [Fact]
    public void ReportCarriesTheBuildOfTheCore()
    {
        var report = new VersionReport(Build);

        Assert.Equal(Build, new BuildInfo(report.CoreVersion, report.Commit, report.WorkingTree));
    }

    [Fact]
    public void ReportWithTheModelVersionGivenCarriesItAndKeepsTheRest()
    {
        var report = new VersionReport(Build).WithModelVersion(VersionValue.Present("pt-model-3.2"));

        Assert.Equal(
            (VersionValue.Present("pt-model-3.2"), Build),
            (report.ModelVersion, new BuildInfo(report.CoreVersion, report.Commit, report.WorkingTree)));
    }

    [Fact]
    public void ReportWithTheModelVersionGivenLeavesTheOriginalAsItWas()
    {
        var original = new VersionReport(Build);

        _ = original.WithModelVersion(VersionValue.Present("pt-model-3.2"));

        Assert.Equal(VersionValue.Absent, original.ModelVersion);
    }

    [Fact]
    public void ReportWithTheSchemaVersionGivenCarriesItAndKeepsTheRest()
    {
        var report = new VersionReport(Build)
            .WithModelVersion(VersionValue.Present("pt-model-3.2"))
            .WithSchemaVersion(VersionValue.Present("4"));

        Assert.Equal(VersionValue.Present("4"), report.SchemaVersion);
        Assert.Equal(VersionValue.Present("pt-model-3.2"), report.ModelVersion);
        Assert.Equal(Build, new BuildInfo(report.CoreVersion, report.Commit, report.WorkingTree));
    }

    [Fact]
    public void ReportWithTheSchemaVersionGivenLeavesTheOriginalAsItWas()
    {
        var original = new VersionReport(Build);

        _ = original.WithSchemaVersion(VersionValue.Present("4"));

        Assert.Equal(VersionValue.Absent, original.SchemaVersion);
    }

    [Fact]
    public void SchemaVersionIsWrittenAsAJsonString()
    {
        var json = JsonSerializer.Serialize(new VersionReport(Build).WithSchemaVersion(VersionValue.Present("4")));

        Assert.EndsWith("\"SchemaVersion\":\"4\"}", json, StringComparison.Ordinal);
    }

    [Fact]
    public void VersionsTheCoreCannotObtainYetAreWrittenAsJsonNull()
    {
        var json = JsonSerializer.Serialize(new VersionReport(Build));

        Assert.EndsWith(
            "\"ContractVersion\":null,\"LanguageServiceVersion\":null,\"ModelVersion\":null,\"SchemaVersion\":null}",
            json,
            StringComparison.Ordinal);
    }
}
