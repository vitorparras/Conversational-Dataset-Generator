using System.Text.Json;
using Core.Host.Endpoints;
using Core.Platform.Versioning;

namespace Core.Tests.Unit.Platform.Versioning;

/// <summary>Issue 51 CA-2: a version not obtained is absent, distinct from empty and from any value.</summary>
public sealed class VersionReportTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingObtainedIsAbsentNeverEmpty(string? obtained)
    {
        var value = VersionValue.OfOrAbsent(obtained);

        Assert.Same(Absent.Instance, value);
    }

    [Fact]
    public void KnownVersionCannotBeEmpty() =>
        Assert.Throws<ArgumentException>(() => VersionValue.Of(string.Empty));

    [Fact]
    public void AbsentSerializesAsItsOwnStateWithoutAValue()
    {
        var report = new VersionReport(
            VersionValue.Of("0.1.0"), VersionValue.Of("abc123"), VersionValue.Of("false"),
            Absent.Instance, Absent.Instance, Absent.Instance);

        var json = JsonSerializer.Serialize(VersionsEndpoint.Handle(report), Web);

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("""{"state":"known","value":"0.1.0"}""", root.GetProperty("core").GetRawText());
        Assert.Equal("""{"state":"absent"}""", root.GetProperty("model").GetRawText());
        Assert.Equal("""{"state":"absent"}""", root.GetProperty("schema").GetRawText());
        Assert.Equal("""{"state":"absent"}""", root.GetProperty("contract").GetRawText());
        Assert.Equal(
            ["core", "commit", "dirtyTree", "contract", "model", "schema"],
            root.EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void DescriptionNamesEveryFieldAndSaysAbsent()
    {
        var report = new VersionReport(
            VersionValue.Of("0.1.0"), Absent.Instance, Absent.Instance,
            Absent.Instance, Absent.Instance, Absent.Instance);

        Assert.Equal(
            "core=0.1.0 commit=absent dirtyTree=absent contract=absent model=absent schema=absent",
            report.Describe());
    }

    [Fact]
    public void ReportFromTheBuildHasContractModelAndSchemaAbsentAtThisStage()
    {
        var report = VersionReport.FromBuild();

        Assert.Equal(VersionValue.Of("0.1.0"), report.Core);
        Assert.Same(Absent.Instance, report.Contract);
        Assert.Same(Absent.Instance, report.Model);
        Assert.Same(Absent.Instance, report.Schema);
    }
}
