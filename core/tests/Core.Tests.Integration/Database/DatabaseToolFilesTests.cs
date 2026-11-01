using Core.Persistence.Tests.Fixtures;
using Core.Tests.Integration.Fixtures;

namespace Core.Tests.Integration.Database;

/// <summary>What tools/database promises in its files, checked without Docker.</summary>
public sealed class DatabaseToolFilesTests
{
    [Fact]
    public void DockerfilePinsTheBaseImageByTagAndDigest()
    {
        var lines = DatabaseTool.ReadLines("Dockerfile");

        var from = Assert.Single(lines, line => line.StartsWith("FROM ", StringComparison.Ordinal));

        Assert.Matches(@"^FROM postgres:17\.\d+-bookworm@sha256:[0-9a-f]{64}$", from);
    }

    [Fact]
    public void DockerfilePinsThePgauditPackageVersion()
    {
        var dockerfile = DatabaseTool.ReadText("Dockerfile");

        Assert.Matches(@"""postgresql-17-pgaudit=\d+\.\d+-\S+""", dockerfile);
    }

    [Fact]
    public void DockerfileSetsTheAuditToReadAndWriteWithoutParametersOrErrorDetail()
    {
        var dockerfile = DatabaseTool.ReadText("Dockerfile");

        Assert.Contains("\"pgaudit.log = 'read, write'\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"pgaudit.log_parameter = off\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"log_parameter_max_length = 0\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"log_parameter_max_length_on_error = 0\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"log_min_messages = log\"", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void DockerfileWritesTheServerLogAsCsvlogOutsideTheDataDirectory()
    {
        var dockerfile = DatabaseTool.ReadText("Dockerfile");

        Assert.Contains("\"log_destination = 'csvlog'\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"log_directory = '/var/log/postgresql'\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"log_truncate_on_rotation = off\"", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeKeepsTheServerLogOnAVolumeOfItsOwn()
    {
        var compose = DatabaseTool.ReadText("compose.yml");

        Assert.Contains("- database-log:/var/log/postgresql", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposePublishesTheServerOnLoopbackOnly()
    {
        var ports = ComposeEntries.Under(DatabaseTool.ReadLines("compose.yml"), "ports");

        Assert.NotEmpty(ports);
        Assert.All(ports, port => Assert.StartsWith("127.0.0.1:", port, StringComparison.Ordinal));
    }

    [Fact]
    public void ComposeTakesEveryEnvironmentValueFromARequiredVariableOfTheSameName()
    {
        var environment = ComposeEntries.Under(DatabaseTool.ReadLines("compose.yml"), "environment");

        Assert.NotEmpty(environment);
        Assert.All(environment, entry => Assert.Matches(@"^(\w+): \$\{\1:\?[^}]+\}$", entry));
    }

    /// <summary>
    /// The core reads every variable that starts with its prefix (<c>EnvironmentSource.Prefix</c>) and refuses the ones it
    /// does not declare, so a compose variable under that prefix would stop the core started from the same shell.
    /// </summary>
    [Fact]
    public void ComposeVariablesStayOutsideTheCorePrefix()
    {
        var environment = ComposeEntries.Under(DatabaseTool.ReadLines("compose.yml"), "environment");

        Assert.NotEmpty(environment);
        Assert.All(environment, entry => Assert.False(entry.StartsWith("CDG_", StringComparison.OrdinalIgnoreCase), entry));
    }

    [Fact]
    public void InitScriptCreatesTheOwnerAndTheApplicationRoles()
    {
        var script = DatabaseTool.ReadText("init/01-database.sh");

        Assert.Contains("CREATE ROLE owner ", script, StringComparison.Ordinal);
        Assert.Contains("CREATE ROLE app ", script, StringComparison.Ordinal);
    }

    [Fact]
    public void InitScriptTakesEveryPasswordFromAPsqlVariable()
    {
        var script = DatabaseTool.ReadText("init/01-database.sh");

        Assert.DoesNotMatch(@"(?i)\bPASSWORD\s+(?!:')", script);
    }
}
