using Core.Host.Migration;
using Core.Persistence.Migrations;
using Microsoft.Extensions.Logging;

namespace Core.Host.Tests.Migration;

/// <summary>The report writes one line for each step applied and one for the outcome, with what a person needs to act, and no connection.</summary>
public sealed class MigrationReportTests
{
    [Fact]
    public void AppliedWritesOneLineForEachStepInOrderThenTheVersionReached()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.Applied, Found = 2, Expected = 2, Applied = ["0001-a", "0002-b"] });

        Assert.Equal([300, 300, 301], log.Lines.Select(line => line.EventId));
        Assert.Equal("Passo 0001-a aplicado e registrado", log.Lines[0].Message);
        Assert.Equal("Passo 0002-b aplicado e registrado", log.Lines[1].Message);
        Assert.Equal("Esquema migrado até a versão 2", log.Lines[2].Message);
    }

    [Fact]
    public void NothingToDoWritesTheVersionTheSchemaIsAt()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.NothingToDo, Found = 3, Expected = 3 });

        var line = Assert.Single(log.Lines);
        Assert.Equal((302, LogLevel.Information, "Nada a migrar: o esquema já está na versão 3"), line);
    }

    [Fact]
    public void RefusedAheadWritesBothVersionsAndSaysNothingWasApplied()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.RefusedAhead, Found = 5, Expected = 3 });

        var line = Assert.Single(log.Lines);
        Assert.Equal(303, line.EventId);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("versão 5, à frente da 3", line.Message, StringComparison.Ordinal);
        Assert.Contains("nada foi aplicado", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusedDivergentWritesTheStepAndBothDigests()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.RefusedDivergent, Step = "0002-b", Registered = "aaa111", Current = "bbb222" });

        var line = Assert.Single(log.Lines);
        Assert.Equal(304, line.EventId);
        Assert.Equal(LogLevel.Error, line.Level);
        Assert.Contains("o passo 0002-b", line.Message, StringComparison.Ordinal);
        Assert.Contains("registrado aaa111, atual bbb222", line.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StepFailedWritesTheStepsThatWereAppliedThenTheFailedOneWithItsSqlStateAndVersion()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.StepFailed, Found = 1, Step = "0002-b", SqlState = "22012", Applied = ["0001-a"] });

        Assert.Equal([300, 305], log.Lines.Select(line => line.EventId));
        Assert.Equal(LogLevel.Error, log.Lines[1].Level);
        Assert.Contains("passo 0002-b (SQLSTATE 22012)", log.Lines[1].Message, StringComparison.Ordinal);
        Assert.Contains("versão 1", log.Lines[1].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StepFailedWithoutSqlStateSaysSo()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.StepFailed, Step = "0001-a" });

        Assert.Contains("SQLSTATE sem SQLSTATE", Assert.Single(log.Lines).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NotConnectedWritesTheSqlStateAndWhatToCheck()
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = MigrationOutcome.NotConnected, SqlState = "28P01" });

        var line = Assert.Single(log.Lines);
        Assert.Equal((306, LogLevel.Error), (line.EventId, line.Level));
        Assert.Contains("(SQLSTATE 28P01)", line.Message, StringComparison.Ordinal);
        Assert.Contains("confira o endereço, o banco, o papel e a senha de dono", line.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MigrationOutcome.Applied)]
    [InlineData(MigrationOutcome.NothingToDo)]
    [InlineData(MigrationOutcome.RefusedAhead)]
    [InlineData(MigrationOutcome.RefusedDivergent)]
    [InlineData(MigrationOutcome.StepFailed)]
    [InlineData(MigrationOutcome.NotConnected)]
    public void NoOutcomeWritesAConnectionString(MigrationOutcome outcome)
    {
        var log = new MigrationLog();

        log.Write(new MigrationResult { Outcome = outcome, Step = "0001-a", Registered = "aaa", Current = "bbb", SqlState = "28P01", Applied = ["0001-a"] });

        Assert.NotEmpty(log.Lines);
        Assert.DoesNotContain(log.Lines, line => line.Message.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(log.Lines, line => line.Message.Contains("Host=", StringComparison.OrdinalIgnoreCase));
    }
}
