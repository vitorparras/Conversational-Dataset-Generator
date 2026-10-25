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
    public void VersionsTheCoreCannotObtainYetAreWrittenAsJsonNull()
    {
        var json = JsonSerializer.Serialize(new VersionReport(Build));

        Assert.EndsWith(
            "\"ContractVersion\":null,\"LanguageServiceVersion\":null,\"ModelVersion\":null,\"SchemaVersion\":null}",
            json,
            StringComparison.Ordinal);
    }
}
