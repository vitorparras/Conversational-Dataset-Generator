namespace Core.Sample.Tests;

public sealed class SetUpWithAStandInTests
{
    private readonly RunningService service = StandInService.Start();

    [Fact]
    public void FindsTheStandInStartedBeforeEveryTest() => Assert.NotNull(service);
}

public sealed class SetUpWithAnArgumentTests
{
    private readonly RunningService service = StandInService.Start("python");

    [Fact]
    public void StartsTheStandInOnAnInterpreterThatIsGiven() => Assert.NotNull(service);
}

public sealed class CreatesATypeThatHoldsTheInterpreterTests
{
    [Fact]
    public void CreatesIt() => _ = new HoldsTheInterpreter();
}

internal sealed class HoldsTheInterpreter
{
    private readonly string interpreter = LanguageServiceInterpreter.Path;
}
