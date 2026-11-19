using Core.Platform.Startup;

namespace Core.Ingestion.Sanitizing;

/// <summary>Refuses to start while a declared step does not say what it alters and what it leaves, and names the first one that does not.</summary>
public sealed class StepsHaveScope : IStartupCheck
{
    private const string Source = "a declaração do passo, na lista de passos da sanitização";

    private readonly SanitizationSteps steps;

    /// <summary>The check over the declared list.</summary>
    /// <param name="steps">The list every material goes through.</param>
    public StepsHaveScope(SanitizationSteps steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        this.steps = steps;
    }

    /// <inheritdoc />
    public string Name => "SanitizationStepScopes";

    /// <inheritdoc />
    public Task<CheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var undeclared = steps.Steps.FirstOrDefault(step => step.Scope is not { IsDeclared: true });
        var result = undeclared is null
            ? CheckResult.Passed(Name, $"os {steps.Steps.Count} passos da sanitização declaram o que alteram e o que deixam", Source)
            : CheckResult.Failed(Name, $"o passo {undeclared.Name} não declara o que altera e o que deixa", Source);
        return Task.FromResult(result);
    }
}
