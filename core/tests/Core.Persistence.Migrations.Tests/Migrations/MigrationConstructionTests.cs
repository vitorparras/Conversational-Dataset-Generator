using System.Text;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.ServerGuarantees;

namespace Core.Persistence.Tests.Migrations;

/// <summary>The runner and the version check are built over a data source and the steps, and neither can be missing.</summary>
public sealed class MigrationConstructionTests
{
    private static readonly IReadOnlyList<MigrationStep> OneStep = [MigrationStep.Of(1, "0001-x", Encoding.UTF8.GetBytes("a"))];

    [Fact]
    public void RunnerWithoutADataSourceIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new MigrationRunner(null!, OneStep));

        Assert.Equal("dataSource", defect.ParamName);
    }

    [Fact]
    public async Task RunnerWithoutStepsIsADefect()
    {
        await using var server = new UnreachableServer();

        var defect = Assert.Throws<ArgumentNullException>(() => new MigrationRunner(server.DataSource, null!));

        Assert.Equal("steps", defect.ParamName);
    }

    [Fact]
    public void VersionCheckWithoutADataSourceIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => new SchemaVersionCheck(null!, OneStep));

        Assert.Equal("dataSource", defect.ParamName);
    }

    [Fact]
    public async Task VersionCheckWithoutStepsIsADefect()
    {
        await using var server = new UnreachableServer();

        var defect = Assert.Throws<ArgumentNullException>(() => new SchemaVersionCheck(server.DataSource, null!));

        Assert.Equal("steps", defect.ParamName);
    }
}
