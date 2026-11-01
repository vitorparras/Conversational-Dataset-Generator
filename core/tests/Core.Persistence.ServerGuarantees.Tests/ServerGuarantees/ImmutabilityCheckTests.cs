using Core.Persistence.ServerGuarantees;
using Core.Platform.Startup;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The triggers that keep a custody event from being changed, removed or emptied away must be on the table, fire in every session and call the functions the migration wrote;
/// each way they are not says which one and the command that puts it back.
/// </summary>
public sealed class ImmutabilityCheckTests
{
    private const string Changed = "custody_event_never_changed";
    private const string Emptied = "custody_event_never_emptied";
    private const string OtherBody = "\nBEGIN\n    RETURN OLD;\nEND\n";

    private static readonly ImmutabilityCheck.TriggerReading ChangedHolding = ImmutabilityCheck.TriggerReading.Declared("A", ImmutabilityCheck.ChangedBody);
    private static readonly ImmutabilityCheck.TriggerReading EmptiedHolding = ImmutabilityCheck.TriggerReading.Declared("A", ImmutabilityCheck.EmptiedBody);

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
    public void BothTriggersEnabledForEverySessionWithTheFunctionsOfTheMigrationPass()
    {
        var finding = ImmutabilityCheck.Judge(ChangedHolding, EmptiedHolding);

        Assert.Equal(CheckStatus.Passed, finding.Status);
        Assert.Contains("public.custody_event", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("D", "A", Changed)]
    [InlineData("A", "D", Emptied)]
    public void DisabledTriggerFailsNamingItAndTheCommandThatEnablesIt(string changed, string emptied, string trigger)
    {
        var finding = ImmutabilityCheck.Judge(
            ImmutabilityCheck.TriggerReading.Declared(changed, ImmutabilityCheck.ChangedBody),
            ImmutabilityCheck.TriggerReading.Declared(emptied, ImmutabilityCheck.EmptiedBody));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"o gatilho {trigger} de public.custody_event está desabilitado", finding.Message, StringComparison.Ordinal);
        Assert.Contains($"ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER {trigger}", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, Changed)]
    [InlineData(false, Emptied)]
    public void AbsentTriggerFailsNamingItAndTheMigrationThatDeclaresIt(bool changedAbsent, string trigger)
    {
        var finding = ImmutabilityCheck.Judge(
            changedAbsent ? ImmutabilityCheck.TriggerReading.Absent : ChangedHolding,
            changedAbsent ? EmptiedHolding : ImmutabilityCheck.TriggerReading.Absent);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"o gatilho {trigger} não está em public.custody_event como a migração 0009 o declara", finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("O")]
    [InlineData("R")]
    public void TriggerThatSkipsSomeSessionsFailsEvenWhenEnabled(string mode)
    {
        var finding = ImmutabilityCheck.Judge(ImmutabilityCheck.TriggerReading.Declared(mode, ImmutabilityCheck.ChangedBody), EmptiedHolding);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains($"o gatilho {Changed} de public.custody_event não dispara em toda sessão", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Emptied, finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BothTriggersWrongAreBothNamed()
    {
        var finding = ImmutabilityCheck.Judge(ImmutabilityCheck.TriggerReading.Absent, ImmutabilityCheck.TriggerReading.Declared("D", ImmutabilityCheck.EmptiedBody));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains(Changed, finding.Message, StringComparison.Ordinal);
        Assert.Contains(Emptied, finding.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "refuse_custody_event_change", Changed)]
    [InlineData(false, "refuse_custody_event_emptying", Emptied)]
    public void FunctionWithAnotherBodyFailsNamingItAndTheTriggerThatCallsItWithoutShowingTheBody(bool changed, string function, string trigger)
    {
        var finding = ImmutabilityCheck.Judge(
            changed ? ImmutabilityCheck.TriggerReading.Declared("A", OtherBody) : ChangedHolding,
            changed ? EmptiedHolding : ImmutabilityCheck.TriggerReading.Declared("A", OtherBody));

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains(
            $"a função public.{function}, que o gatilho {trigger} chama, não tem o corpo nem a linguagem que a migração 0009 escreveu", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("RETURN OLD", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionInAnotherLanguageFailsEvenWithTheBodyOfTheMigration()
    {
        var finding = ImmutabilityCheck.Judge(new ImmutabilityCheck.TriggerReading("A", ImmutabilityCheck.ChangedBody, "sql"), EmptiedHolding);

        Assert.Equal(CheckStatus.Failed, finding.Status);
        Assert.Contains("a função public.refuse_custody_event_change", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FunctionChangedAndTriggerDisabledAreBothNamed()
    {
        var finding = ImmutabilityCheck.Judge(new ImmutabilityCheck.TriggerReading("D", OtherBody, ImmutabilityCheck.ExpectedLanguage), EmptiedHolding);

        Assert.Contains("está desabilitado", finding.Message, StringComparison.Ordinal);
        Assert.Contains("a função public.refuse_custody_event_change", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckWithoutADataSourceIsADefect() =>
        _ = Assert.Throws<ArgumentNullException>(() => new ImmutabilityCheck(null!));
}
