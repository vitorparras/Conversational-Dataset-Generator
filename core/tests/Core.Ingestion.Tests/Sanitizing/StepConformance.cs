using System.Text;
using Core.Boundary.Contracts;
using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// The conformance every registered step answers to: each difference between the text a step received and the one it left falls inside an edit it returned, and each edit falls
/// inside the scope the step declares, in a form a check can read. A step of the module without a scope here fails the conformance before anything else (issue 96).
/// </summary>
internal static class StepConformance
{
    public const string OutOfScope = "fora do escopo";
    public const string WithoutEdit = "sem edição devolvida";

    private const char LineBreak = '\n';
    private const char PageBreak = '\f';

    private static readonly Lazy<Encoding> Windows1252 = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    });

    /// <summary>The declared scope of each step of the module, in the order the steps run.</summary>
    public static IReadOnlyList<(string Step, ConformanceScope Scope)> Scopes { get; } =
    [
        ("codificacao", new ConformanceScope("o caractere que a codificação errada leu como sequência latina", IsMisreading)),
        ("artefatos", new ConformanceScope("uma linha inteira, retirada sem escrever nada", IsWholeLineTakenAway)),
        ("identificadores", new ConformanceScope("um identificador trocado pelo marcador [documento] ou [contato]", (_, written) => written is "[documento]" or "[contato]")),
        ("destinos",new ConformanceScope("uma ocorrência trocada pelo marcador [credencial] ou [dado]", (_, written) => written is "[credencial]" or "[dado]")),
    ];

    /// <summary>The scope declared for the step, or none when the step is not covered.</summary>
    public static ConformanceScope? ScopeOf(string step) => Scopes.FirstOrDefault(scope => scope.Step == step).Scope;

    /// <summary>Every step of the module: each class of Core.Ingestion that is a step.</summary>
    public static IReadOnlyList<ISanitizationStep> Registered() =>
    [
        .. typeof(ISanitizationStep).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(ISanitizationStep)))
            .Select(type => (ISanitizationStep)(type.GetConstructor([typeof(ITextBoundaries)])?.Invoke([Sanitizing.Boundaries]) ?? Activator.CreateInstance(type)!))
            .OrderBy(step => Scopes.Select(scope => scope.Step).ToList().IndexOf(step.Name)),
    ];

    /// <summary>What a step did to one text, as the sanitizer applied it.</summary>
    public static StepRun RunOf(ISanitizationStep step, string text, params DecidedOccurrence[] decided)
    {
        var sanitized = Sanitizing.Sanitized(Sanitizing.Sanitizer(step), text, decided);
        return new StepRun(text, sanitized.Report[0].Edits, sanitized.Text);
    }

    /// <summary>Each difference not covered by a returned edit, and each edit outside the scope, with its span in the received text and the scope it violates.</summary>
    public static IReadOnlyList<Violation> Check(ConformanceScope scope, StepRun run)
    {
        var walk = new Walk(Runes(run.Input), Runes(run.Output), scope);
        foreach (var edit in run.Edits)
        {
            walk.Unchanged(edit.Start);
            walk.Edited(edit);
        }

        walk.Unchanged(walk.InputLength);
        walk.Ended();
        return walk.Violations;
    }

    /// <summary>The text as a program that read its UTF-8 bytes as Windows-1252 leaves it.</summary>
    public static string Misread(string text) => Windows1252.Value.GetString(Encoding.UTF8.GetBytes(text));

    /// <summary>Whether the written character, misread one or more times as Windows-1252, gives the replaced sequence.</summary>
    private static bool IsMisreading(string replaced, string written)
    {
        var misread = written;
        for (var time = 0; time < 3 && written.EnumerateRunes().Count() == 1; time++)
        {
            misread = Misread(misread);
            if (misread == replaced)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWholeLineTakenAway(string replaced, string written) =>
        written.Length == 0 && !string.IsNullOrWhiteSpace(replaced) && !replaced.Contains(PageBreak, StringComparison.Ordinal)
            && !replaced.TrimEnd(LineBreak).Contains(LineBreak, StringComparison.Ordinal);

    private static int[] Runes(string text) => [.. text.EnumerateRunes().Select(rune => rune.Value)];

    /// <summary>A walk over the received text and the text left, edit by edit.</summary>
    private sealed class Walk(int[] input, int[] output, ConformanceScope scope)
    {
        private int from;
        private int at;

        public List<Violation> Violations { get; } = [];

        public int InputLength => input.Length;

        private static string Text(int[] runes, int start, int end) => string.Concat(runes[start..end].Select(char.ConvertFromUtf32));

        /// <summary>The received text up to the position given must reappear as it was.</summary>
        public void Unchanged(int until)
        {
            var differing = Enumerable.Range(from, until - from).Where(index => at + index - from >= output.Length || output[at + index - from] != input[index]).ToList();
            if (differing.Count > 0)
            {
                Violations.Add(new Violation(differing[0], differing[^1] + 1, scope.Declared, WithoutEdit));
            }

            at += until - from;
            from = until;
        }

        /// <summary>The edit must have written what it says, and fall inside the scope.</summary>
        public void Edited(SanitizationEdit edit)
        {
            var written = Runes(edit.Written);
            var left = output.Skip(at).Take(written.Length);
            if (!left.SequenceEqual(written))
            {
                Violations.Add(new Violation(edit.Start, edit.End, scope.Declared, WithoutEdit));
            }

            if (!scope.Allows(Text(input, edit.Start, edit.End), edit.Written))
            {
                Violations.Add(new Violation(edit.Start, edit.End, scope.Declared, OutOfScope));
            }

            at += written.Length;
            from = edit.End;
        }

        /// <summary>Nothing may have been added past the end of the received text.</summary>
        public void Ended()
        {
            if (at != output.Length)
            {
                Violations.Add(new Violation(input.Length, input.Length, scope.Declared, WithoutEdit));
            }
        }
    }
}

/// <summary>A scope a check can read: what the step declares it alters, and whether a replaced span and what was written in its place fall inside it.</summary>
internal sealed record ConformanceScope(string Declared, Func<string, string, bool> Allows);

/// <summary>What a step received, the edits it returned and what it left.</summary>
internal sealed record StepRun(string Input, IReadOnlyList<SanitizationEdit> Edits, string Output);

/// <summary>A change that breaks the conformance: its span in the received text, the scope it violates and why.</summary>
internal sealed record Violation(int Start, int End, string Scope, string What);
