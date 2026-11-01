using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// pgaudit must be loaded by the server, installed in the database, log what the application reads and writes, and keep parameters,
/// error detail and query text out of the log without dropping the audit lines; each absence says where it is fixed.
/// </summary>
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
        var finding = PgAuditCheck.Judge(Settings(log: "read, write, ddl"), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Contains("pgaudit 17.0", finding.Message, StringComparison.Ordinal);
        Assert.Contains("pgaudit.log = 'read, write, ddl'", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotLoadedFailsPointingAtSharedPreloadLibraries()
    {
        var finding = PgAuditCheck.Judge(Settings(log: null), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("shared_preload_libraries", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotInstalledFailsWithTheCommandThatInstallsIt()
    {
        var finding = PgAuditCheck.Judge(Settings(), extensionVersion: null);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("CREATE EXTENSION pgaudit", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("read, write")]
    [InlineData("WRITE,READ")]
    [InlineData("all")]
    [InlineData("none, read, write")]
    [InlineData("all, -ddl")]
    public void LogCoveringReadAndWritePasses(string log)
    {
        var finding = PgAuditCheck.Judge(Settings(log: log), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Passed, finding.Status);
    }

    [Theory]
    [InlineData("write, ddl", "read")]
    [InlineData("read", "write")]
    [InlineData("all, -write", "write")]
    [InlineData("read, write, -read", "read")]
    [InlineData("none", "read e write")]
    public void LogMissingAClassFailsNamingIt(string log, string missing)
    {
        var finding = PgAuditCheck.Judge(Settings(log: log), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"não registra {missing} do papel da aplicação", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParametersInTheAuditLogFailNamingTheSetting()
    {
        var finding = PgAuditCheck.Judge(Settings(logParameter: "on"), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("defina pgaudit.log_parameter = off", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("64")]
    [InlineData(null)]
    public void ParametersInTheStatementLogFailNamingTheSetting(string? length)
    {
        var finding = PgAuditCheck.Judge(Settings(parameterMaxLength: length), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("defina log_parameter_max_length = 0", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParametersInTheErrorLogFailNamingTheSetting()
    {
        var finding = PgAuditCheck.Judge(Settings(parameterMaxLengthOnError: "-1"), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("defina log_parameter_max_length_on_error = 0", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData(null)]
    public void ErrorsInTheServerLogFailNamingTheSetting(string? level)
    {
        var finding = PgAuditCheck.Judge(Settings(minMessages: level), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("defina log_min_messages = log", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void LevelLogKeepsTheAuditAndDropsTheErrorsAndPasses()
    {
        var finding = PgAuditCheck.Judge(Settings(minMessages: "LOG"), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Passed, finding.Status);
    }

    [Theory]
    [InlineData("fatal")]
    [InlineData("FATAL")]
    [InlineData("panic")]
    public void LevelAboveLogFailsSayingItDropsTheAudit(string level)
    {
        var finding = PgAuditCheck.Judge(Settings(minMessages: level), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("descarta as linhas LOG", finding.Message, StringComparison.Ordinal);
        Assert.Contains("defina log_min_messages = log", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("debug1")]
    [InlineData("info")]
    [InlineData("notice")]
    [InlineData("WARNING")]
    public void AuditLinesSentToTheClientBelowLogFailNamingTheSetting(string level)
    {
        var finding = PgAuditCheck.Judge(Settings(logClient: "on", logLevel: level), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"pgaudit.log_level = '{level}'", finding.Message, StringComparison.Ordinal);
        Assert.Contains("defina pgaudit.log_client = off", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("on", "log")]
    [InlineData("on", null)]
    [InlineData("off", "debug1")]
    [InlineData(null, "warning")]
    public void ClientSettingsThatKeepTheAuditAtLogPass(string? logClient, string? logLevel)
    {
        var finding = PgAuditCheck.Judge(Settings(logClient: logClient, logLevel: logLevel), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Passed, finding.Status);
    }

    [Fact]
    public void QueryTextPrintedByTheServerFailsNamingTheSettings()
    {
        var finding = PgAuditCheck.Judge(Settings(debugPrints: "debug_print_parse, debug_print_plan"), extensionVersion: "17.0");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("debug_print_parse, debug_print_plan = on", finding.Message, StringComparison.Ordinal);
        Assert.Contains("defina cada uma como off", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryGapIsNamedInTheSameRefusal()
    {
        var finding = PgAuditCheck.Judge(new AuditSettings("ddl", "on", "-1", "-1", "warning", "on", "notice", "debug_print_plan"), extensionVersion: "17.0");

        Assert.Equal(7, finding.Message.Split("; ").Length);
    }

    [Fact]
    public void CheckWithoutADataSourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new PgAuditCheck(null!));

    private static AuditSettings Settings(
        string? log = "read, write",
        string? logParameter = "off",
        string? parameterMaxLength = "0",
        string? parameterMaxLengthOnError = "0",
        string? minMessages = "log",
        string? logClient = "off",
        string? logLevel = "log",
        string? debugPrints = null) =>
        new(log, logParameter, parameterMaxLength, parameterMaxLengthOnError, minMessages, logClient, logLevel, debugPrints);
}
