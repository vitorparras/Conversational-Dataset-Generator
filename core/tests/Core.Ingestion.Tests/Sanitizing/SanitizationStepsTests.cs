using Core.Ingestion.Sanitizing;
using Core.Platform.Startup;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>Every material goes through one declared list of steps, whose order is part of the version it records (issue 87).</summary>
public sealed class SanitizationStepsTests
{
    private const string Raw = "ab QXZ7 cd";

    [Fact]
    public void ResultCarriesSanitizedTextVersionAndStepOrder()
    {
        var first = new FixedStep("primeiro", 3, Declared, _ => []);
        var steps = new SanitizationSteps([first, new DestinationsStep()]);

        var sanitized = Assert.IsType<SanitizedText>(new Sanitizer(steps, Boundaries).Sanitize(Raw, [Data(3, 7)]).Sanitized);

        Assert.Equal("ab [dado] cd", sanitized.Text);
        Assert.Equal(steps.Version, sanitized.Version);
        Assert.Equal([new StepIdentity("primeiro", 3), new StepIdentity("destinos", 1)], sanitized.Order);
    }

    public static TheoryData<string?, string?> Undeclared => new()
    {
        { null, null },
        { string.Empty, "o resto do texto" },
        { "troca o trecho", " " },
    };

    [Theory]
    [MemberData(nameof(Undeclared))]
    public async Task StepWithoutDeclaredScopeIsRefusedAtStartupByName(string? alters, string? leaves)
    {
        var scope = alters is null ? null : new StepScope(alters, leaves!);
        var steps = new SanitizationSteps([new DestinationsStep(), new FixedStep("passo-incompleto", 1, scope, _ => [])]);

        var result = await new StepsHaveScope(steps).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Failed, result.Status);
        Assert.Contains("passo-incompleto", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("destinos", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StepsThatAllDeclareTheirScopePassTheStartupCheck()
    {
        var steps = new SanitizationSteps([FixedStep.Nothing("primeiro"), new DestinationsStep()]);

        var result = await new StepsHaveScope(steps).RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CheckStatus.Passed, result.Status);
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(5, 3)]
    [InlineData(0, 11)]
    public void OccurrenceOutsideTheTextIsRefusedBeforeAnyStep(int start, int end)
    {
        var step = new FixedStep("primeiro", _ => throw new InvalidOperationException("o passo não devia rodar"));

        _ = Assert.Throws<ArgumentException>(() => Sanitizer(step).Sanitize(Raw, [Data(start, end)]));
    }

    [Fact]
    public void ListWithoutItsStepsOrWithAMissingOneIsRefused()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new SanitizationSteps(null!));
        _ = Assert.Throws<ArgumentException>(() => new SanitizationSteps([FixedStep.Nothing("primeiro"), null!]));
    }

    [Fact]
    public void ChangingTheStepOrderChangesTheVersion()
    {
        var before = new SanitizationSteps([FixedStep.Nothing("primeiro"), new DestinationsStep()]);
        var swapped = new SanitizationSteps([new DestinationsStep(), FixedStep.Nothing("primeiro")]);
        var again = new SanitizationSteps([FixedStep.Nothing("primeiro"), new DestinationsStep()]);

        var recorded = Assert.IsType<SanitizedText>(new Sanitizer(swapped, Boundaries).Sanitize(Raw, [Data(3, 7)]).Sanitized).Version;

        Assert.NotEqual(before.Version, recorded);
        Assert.Equal(before.Version, again.Version);
    }

    [Fact]
    public void ChangingTheVersionOfAStepChangesTheVersion()
    {
        var one = new SanitizationSteps([new FixedStep("primeiro", 1, Declared, _ => [])]);
        var two = new SanitizationSteps([new FixedStep("primeiro", 2, Declared, _ => [])]);

        Assert.NotEqual(one.Version, two.Version);
    }

    [Fact]
    public void MaterialWithoutDecidedDestinationsIsRefused()
    {
        var undecided = OfDestinations().Sanitize(Raw, null);
        var nothingFound = OfDestinations().Sanitize(Raw, []);

        Assert.Equal((SanitizationRefusal.Undecided, null), (undecided.Refusal, undecided.Sanitized));
        Assert.Equal((null, Raw), (nothingFound.Refusal, nothingFound.Sanitized?.Text));
    }
}
