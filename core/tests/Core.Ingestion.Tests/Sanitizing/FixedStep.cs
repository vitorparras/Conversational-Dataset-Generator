using Core.Ingestion.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>A step whose edits are given by the test, to see what the sanitizer and the trail do with them.</summary>
internal sealed class FixedStep(string name, int version, StepScope? scope, Func<StepInput, IReadOnlyList<SanitizationEdit>> edits) : ISanitizationStep
{
    public FixedStep(string name, Func<StepInput, IReadOnlyList<SanitizationEdit>> edits)
        : this(name, 1, Sanitizing.Declared, edits)
    {
    }

    public string Name => name;

    public int Version => version;

    public StepScope? Scope => scope;

    public List<IReadOnlyList<SanitizationEdit>> Returned { get; } = [];

    public IReadOnlyList<SanitizationEdit> Edits(StepInput input)
    {
        var returned = edits(input);
        Returned.Add(returned);
        return returned;
    }

    public static FixedStep Nothing(string name) => new(name, _ => []);
}
