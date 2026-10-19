using CsCheck;

namespace Core.Tests.Properties.Harness;

/// <summary>Runs a property for the declared number of cases; a failure carries the seed, the seed variable replays it, a run past its deadline fails.</summary>
internal sealed class PropertyRunner
{
    /// <summary>The variable that fixes the seed; it is also the one the failure message names.</summary>
    public const string SeedVariable = "CsCheck_Seed";

    /// <summary>The variable that trades the number of cases for a time; refused, since the cases are declared.</summary>
    public const string TimeVariable = "CsCheck_Time";

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
        if (!string.IsNullOrWhiteSpace(environment(TimeVariable)))
        {
            throw new InvalidOperationException($"{TimeVariable} replaces the declared {budget.Cases} cases with a duration; unset it.");
        }

        var seed = environment(SeedVariable);
        using var cancellation = new CancellationTokenSource();
        var run = Task.Run(() => Sample(generator, property, string.IsNullOrWhiteSpace(seed) ? null : seed, cancellation.Token));

        if (Task.WaitAny([run], budget.Deadline) < 0)
        {
            cancellation.Cancel();
            throw new TimeoutException($"The property did not finish {budget.Cases} cases within {budget.Deadline}.");
        }

        run.GetAwaiter().GetResult();
    }

    /// <summary>A seeded run replays that one case first, so the shrinking is the one the seed names, then goes on to the declared cases.</summary>
    private void Sample<T>(Gen<T> generator, Action<T> property, string? seed, CancellationToken token)
    {
        void Guarded(T value)
        {
            token.ThrowIfCancellationRequested();
            property(value);
        }

        if (seed is not null)
        {
            generator.Sample(Guarded, iter: 1, seed: seed, threads: 1, writeLine: _ => { });
        }

        generator.Sample(Guarded, iter: budget.Cases, seed: seed, writeLine: report);
    }
}
