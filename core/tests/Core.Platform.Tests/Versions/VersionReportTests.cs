using System.Text.Json;
using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>The report carries the build of the core, the versions the service declares and the schema, and declares absent every version it was not given.</summary>
public sealed class VersionReportTests
{
    private static readonly BuildInfo Build = new(
        VersionValue.Present("9.8.7"), VersionValue.Present("0123456789abcdef0123456789abcdef01234567"), WorkingTree.Dirty);

    private static readonly ServiceVersions Declared = new(
        VersionValue.Present("7"), VersionValue.Present("0.4.2"), VersionValue.Present("pt-model-3.2"));

    [Fact]
    public void ReportCarriesTheBuildOfTheCore()
    {
        var report = new VersionReport(Build);

        Assert.Equal(Build, new BuildInfo(report.CoreVersion, report.Commit, report.WorkingTree));
    }

    [Fact]
    public void ReportWithTheServiceVersionsGivenCarriesEachOneAndKeepsTheRest()
    {
        var report = new VersionReport(Build).WithServiceVersions(Declared);

        Assert.Equal(
            (VersionValue.Present("7"), VersionValue.Present("0.4.2"), VersionValue.Present("pt-model-3.2"), Build),
            (report.ContractVersion, report.LanguageServiceVersion, report.ModelVersion, new BuildInfo(report.CoreVersion, report.Commit, report.WorkingTree)));
    }

    [Fact]
    public void ReportWithTheServiceVersionsGivenLeavesTheOriginalAsItWas()
    {
        var original = new VersionReport(Build);

        _ = original.WithServiceVersions(Declared);

        Assert.Equal(
            (VersionValue.Absent, VersionValue.Absent, VersionValue.Absent),
            (original.ContractVersion, original.LanguageServiceVersion, original.ModelVersion));
    }

    [Fact]
    public void ReportWithNoServiceVersionsHasNoneOfThem()
    {
        var report = new VersionReport(Build).WithServiceVersions(Declared).WithServiceVersions(ServiceVersions.Absent);

        Assert.Equal(
            (VersionValue.Absent, VersionValue.Absent, VersionValue.Absent),
            (report.ContractVersion, report.LanguageServiceVersion, report.ModelVersion));
    }

    [Fact]
    public void ReportWithTheSchemaVersionGivenCarriesItAndKeepsTheRest()
    {
        var report = new VersionReport(Build)
            .WithServiceVersions(Declared)
            .WithSchemaVersion(VersionValue.Present("4"));

        Assert.Equal(VersionValue.Present("4"), report.SchemaVersion);
        Assert.Equal(Declared, new ServiceVersions(report.ContractVersion, report.LanguageServiceVersion, report.ModelVersion));
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
    public void ServiceVersionsAreWrittenAsJsonStringsInTheirPlaces()
    {
        var json = JsonSerializer.Serialize(new VersionReport(Build).WithServiceVersions(Declared));

        Assert.EndsWith(
            "\"ContractVersion\":\"7\",\"LanguageServiceVersion\":\"0.4.2\",\"ModelVersion\":\"pt-model-3.2\",\"SchemaVersion\":null}",
            json,
            StringComparison.Ordinal);
    }

    [Fact]
    public void VersionsTheReportWasNotGivenAreWrittenAsJsonNull()
    {
        var json = JsonSerializer.Serialize(new VersionReport(Build));

        Assert.EndsWith(
            "\"ContractVersion\":null,\"LanguageServiceVersion\":null,\"ModelVersion\":null,\"SchemaVersion\":null}",
            json,
            StringComparison.Ordinal);
    }
}
