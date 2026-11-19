using System.Text;
using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Tests.Support.Harness;
using CsCheck;
using static Core.Ingestion.Tests.Sanitizing.Sanitizing;
using static Core.Ingestion.Tests.Sanitizing.StepConformance;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>No registered step alters the text outside the scope it declares, nor without the edit that says so; a widened scope passes, and is judged by the signal (issue 96).</summary>
public sealed class StepConformanceTests
{
    private const string Identifier = "QZX7KW";

    private static readonly string[] Words = ["ação", "você", "não", "“oi”", "\U0001F600", "é", "—", "ÁGUA"];

    /// <summary>One to four pages of signal, each with a word misread once or twice and an identifier of any destination, under a header and a numbered footer or not.</summary>
    private static readonly Gen<(string Text, DecidedOccurrence[] Decided)> Materials = Gen.Select(
        Gen.Bool,
        Gen.Bool,
        Gen.Select(SignalTexts.Of([.. SignalTexts.Kinds]), Gen.OneOf(Words), Gen.Int[1, 2], Gen.Int[0, 3], (sample, word, times, kind) => (sample.Text, word, times, kind)).Array[1, 4],
        Built);

    private static readonly string Repair = ScopeOf("codificacao")!.Declared;

    /// <summary>A step under the scope of the repair that, besides repairing, evens out typographic quotes.</summary>
    private static FixedStep RepairThatFlattensQuotes() => new("codificacao", input =>
    [
        SanitizationEdit.Rewriting(3, 4, "\""),
        SanitizationEdit.Rewriting(5, 7, "ã"),
        SanitizationEdit.Rewriting(8, 9, "\""),
    ]);

    private const string Quoted = "oi “nÃ£o” fim";

    [Fact]
    [Trait("Kind", "Property")]
    public void EveryDifferenceFallsWithinTheDeclaredScope() =>
        PropertyRunner.FromProcess().Check(Materials, generated =>
        {
            var (text, decided) = generated;

            var violations = Registered().SelectMany(step => Check(ScopeOf(step.Name)!, RunOf(step, text, decided)).Select(violation => (step.Name, violation)));

            Assert.Empty(violations);
        });

    [Fact]
    public void ChangeOutsideTheScopeIsRejectedWithSpanAndScope()
    {
        var violations = Check(ScopeOf("codificacao")!, RunOf(RepairThatFlattensQuotes(), Quoted));

        Assert.Equal([new Violation(3, 4, Repair, OutOfScope), new Violation(8, 9, Repair, OutOfScope)], violations);
    }

    [Fact]
    public void EveryRegisteredStepIsCoveredByTheConformance() =>
        Assert.Equal(Scopes.Select(scope => scope.Step).Order(), Registered().Select(step => step.Name).Order());

    [Fact]
    public void ChangeWithoutReturnedEditIsRejected()
    {
        var silent = new StepRun("abc zqx fim", [], "abc ZQX fim");

        var violations = Check(ScopeOf("codificacao")!, silent);

        Assert.Equal([new Violation(4, 7, Repair, WithoutEdit)], violations);
    }

    [Fact]
    public void WidenedScopePassesTheConformance()
    {
        var repair = ScopeOf("codificacao")!;
        var widened = new ConformanceScope(
            $"{repair.Declared}, e as aspas tipográficas trocadas por retas",
            (replaced, written) => repair.Allows(replaced, written) || (replaced is "“" or "”" && written == "\""));

        var violations = Check(widened, RunOf(RepairThatFlattensQuotes(), Quoted));

        Assert.Empty(violations);
    }

    private static (string Text, DecidedOccurrence[] Decided) Built(bool header, bool footer, (string Text, string Word, int Times, int Kind)[] pages)
    {
        var text = new StringBuilder();
        var decided = new List<DecidedOccurrence>();
        foreach (var (page, at) in pages.Select((page, at) => (page, at)))
        {
            _ = text.Append(at > 0 ? "\f" : string.Empty).Append(header ? "Relatório ZQX\n" : string.Empty).Append(page.Text).Append(' ');
            _ = text.Append(page.Times == 1 ? Misread(page.Word) : Misread(Misread(page.Word))).Append(' ');
            var start = text.ToString().EnumerateRunes().Count();
            decided.Add(OfKind(page.Kind, start, start + Identifier.Length));
            _ = text.Append(Identifier).Append(footer ? $"\nPágina {at + 1} de {pages.Length}" : string.Empty);
        }

        return (text.ToString(), [.. decided]);
    }

    private static DecidedOccurrence OfKind(int kind, int start, int end) => kind switch
    {
        0 => Credential(start, end),
        1 => Data(start, end),
        2 => Pseudonymized(start, end),
        _ => Preserved(start, end),
    };
}
