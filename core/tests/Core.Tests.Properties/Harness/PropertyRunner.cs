using CsCheck;

namespace Core.Tests.Properties.Harness;

/// <summary>Runs a property for the declared number of cases; a failure carries the seed, the seed variable replays it, a run past its deadline fails.</summary>
internal sealed class PropertyRunner
{
    /// <summary>The variable that fixes the seed; it is also the one the failure message names.</summary>
    public const string SeedVariable = "CsCheck_Seed";

    private readonly Func<string, string?> environment;
    private readonly PropertyBudget budget;
    private readonly Action<string> report;

    public PropertyRunner(Func<string, string?> environment, PropertyBudget budget, Action<string> report)
    {
        this.environment = environment;
        this.budget = budget;
        this.report = report;
    }

    public static PropertyRunner FromProcess() =>
        new(Environment.GetEnvironmentVariable, PropertyBudget.Declared, TestContext.Current.SendDiagnosticMessage);

    public void Check<T>(Gen<T> generator, Action<T> property)
    {
        var seed = environment(SeedVariable);
        using var cancellation = new CancellationTokenSource();
        var run = Task.Run(() => generator.Sample(
            value =>
            {
                cancellation.Token.ThrowIfCancellationRequested();
                property(value);
            },
            iter: budget.Cases,
            writeLine: report,
            seed: string.IsNullOrWhiteSpace(seed) ? null : seed));

        if (Task.WaitAny([run], budget.Deadline) < 0)
        {
            cancellation.Cancel();
            throw new TimeoutException($"The property did not finish {budget.Cases} cases within {budget.Deadline}.");
        }

        run.GetAwaiter().GetResult();
    }
}
