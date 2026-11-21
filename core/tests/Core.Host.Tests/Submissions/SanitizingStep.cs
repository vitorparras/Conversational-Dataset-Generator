using Core.Boundary.Text;
using Core.Ingestion.Sanitizing;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// A declared step for the tests: one that fails on the item at a given call or of a given text, one that cancels the submission there, or one that removes every occurrence as a
/// credential.
/// </summary>
internal sealed class SanitizingStep : ISanitizationStep
{
    private readonly Func<int, StepInput, IReadOnlyList<SanitizationEdit>> answer;
    private int calls;

    private SanitizingStep(Func<int, StepInput, IReadOnlyList<SanitizationEdit>> answer)
    {
        this.answer = answer;
    }

    public string Name => "passo_de_teste";

    public int Version => 1;

    public StepScope? Scope { get; } = new("o que o teste pede", "o resto");

    /// <summary>How many items the step received.</summary>
    public int Calls => calls;

    /// <summary>Fails on the item of the given call, counted from one; removes nothing before it.</summary>
    internal static SanitizingStep FailingAt(int call) =>
        new((at, _) => at == call ? throw new InvalidOperationException("falha sintética do passo") : []);

    /// <summary>Fails on every item whose text holds the one given; removes nothing in the others.</summary>
    internal static SanitizingStep FailingOn(string text) =>
        new((_, input) => input.Text.Contains(text, StringComparison.Ordinal) ? throw new InvalidOperationException("falha sintética do passo") : []);

    /// <summary>Cancels the submission on the item of the given call, counted from one, as a cancellation would arrive while sanitizing.</summary>
    internal static SanitizingStep CancellingAt(int call, CancellationTokenSource cancelled) =>
        new((at, _) =>
        {
            if (at == call)
            {
                cancelled.Cancel();
                cancelled.Token.ThrowIfCancellationRequested();
            }

            return [];
        });

    /// <summary>Removes every occurrence as a credential.</summary>
    internal static SanitizingStep RemovingAll() =>
        new((_, input) => [.. input.Occurrences.OrderBy(decided => decided.Occurrence.Start)
            .Select(decided => new SanitizationEdit(decided.Occurrence.Start, decided.Occurrence.End, SanitizationMarker.Credential))]);

    /// <summary>The sanitizer that runs this step alone.</summary>
    internal Sanitizer Alone() => new(new SanitizationSteps([this]), new TextBoundaries());

    public IReadOnlyList<SanitizationEdit> Edits(StepInput input) => answer(Interlocked.Increment(ref calls), input);
}
