using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>pgaudit must be loaded by the server and installed in the database; each absence says where it is fixed.</summary>
public sealed class PgAuditCheckTests
{
    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new PgAuditCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("PgAudit", result, server);
    }

    [Fact]
    public void LoadedAndInstalledPassesShowingVersionAndSetting()
    {
        var finding = PgAuditCheck.Judge(logSetting: "write, ddl", extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Contains("pgaudit 17.0", finding.Message, StringComparison.Ordinal);
        Assert.Contains("pgaudit.log = 'write, ddl'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotLoadedFailsPointingAtSharedPreloadLibraries()
    {
        var finding = PgAuditCheck.Judge(logSetting: null, extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("shared_preload_libraries", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotInstalledFailsWithTheCommandThatInstallsIt()
    {
        var finding = PgAuditCheck.Judge(logSetting: "none", extensionVersion: null);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("CREATE EXTENSION pgaudit", finding.Message, StringComparison.Ordinal);
    }
}
