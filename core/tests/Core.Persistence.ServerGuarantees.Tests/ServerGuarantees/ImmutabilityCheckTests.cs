using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The triggers that keep a custody event from being changed, removed or emptied away must be on the table and fire in every session;
/// each way they are not says which one and the command that puts it back.
/// </summary>
public sealed class ImmutabilityCheckTests
{
    private const string Changed = "custody_event_never_changed";
    private const string Emptied = "custody_event_never_emptied";

    [Fact]
    public async Task UnreachableServerIsARefusalNamingTheCheck()
    {
        await using var server = new UnreachableServer();
        var check = new ImmutabilityCheck(server.DataSource);

        var result = await check.RunAsync(TestContext.Current.CancellationToken);

        RefusalAssert.NamesTheCheckAndTheAddressWithoutTheSecret("Immutability", result, server);
    }

    [Fact]
    public async Task CheckHasTheNameTheStartupReportShows()
    {
        await using var server = new UnreachableServer();

        Assert.Equal("Immutability", new ImmutabilityCheck(server.DataSource).Name);
    }

    [Fact]
    public void BothTriggersEnabledForEverySessionPass()
    {
        var finding = ImmutabilityCheck.Judge("A", "A");

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Contains("public.custody_event", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("D", "A", Changed)]
    [InlineData("A", "D", Emptied)]
    public void DisabledTriggerFailsNamingItAndTheCommandThatEnablesIt(string changed, string emptied, string trigger)
    {
        var finding = ImmutabilityCheck.Judge(changed, emptied);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"o gatilho {trigger} de public.custody_event está desabilitado", finding.Message, StringComparison.Ordinal);
        Assert.Contains($"ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "A", Changed)]
    [InlineData("A", null, Emptied)]
    public void AbsentTriggerFailsNamingItAndTheMigrationThatDeclaresIt(string? changed, string? emptied, string trigger)
    {
        var finding = ImmutabilityCheck.Judge(changed, emptied);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"o gatilho {trigger} não está em public.custody_event como a migração 0009 o declara", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("O")]
    [InlineData("R")]
    public void TriggerThatSkipsSomeSessionsFailsEvenWhenEnabled(string mode)
    {
        var finding = ImmutabilityCheck.Judge(mode, "A");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"o gatilho {Changed} de public.custody_event não dispara em toda sessão", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Emptied, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BothTriggersWrongAreBothNamed()
    {
        var finding = ImmutabilityCheck.Judge(null, "D");

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains(Changed, finding.Message, StringComparison.Ordinal);
        Assert.Contains(Emptied, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckWithoutADataSourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new ImmutabilityCheck(null!));
}
