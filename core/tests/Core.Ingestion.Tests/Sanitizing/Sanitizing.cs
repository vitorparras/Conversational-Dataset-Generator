using Core.Boundary.Contracts;
using Core.Boundary.Text;
using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>Decided occurrences, steps and sanitizers for the tests of the sanitization, over texts made of synthetic identifiers.</summary>
internal static class Sanitizing
{
    public static TextBoundaries Boundaries { get; } = new();

    public static StepScope Declared { get; } = new("troca o trecho de teste por um marcador", "o resto do texto");

    /// <summary>An occurrence with the destination the policy gives it, as the entrance decides.</summary>
    public static DecidedOccurrence Decided(string kind, string partOfSpeech, int start, int end)
    {
        var occurrence = new Occurrence(start, end, kind, partOfSpeech, "obj");
        return new DecidedOccurrence(occurrence, DestinationPolicy.Of(occurrence));
    }

    /// <summary>A secret, which the policy removes.</summary>
    public static DecidedOccurrence Credential(int start, int end) => Decided("TOKEN", "NOUN", start, end);

    /// <summary>A kind that is not a proper name here, which the policy removes as any other data.</summary>
    public static DecidedOccurrence Data(int start, int end) => Decided(OccurrenceKinds.Person, "NOUN", start, end);

    /// <summary>A proper name of a person, which the policy pseudonymizes.</summary>
    public static DecidedOccurrence Pseudonymized(int start, int end) => Decided(OccurrenceKinds.Person, "PROPN", start, end);

    /// <summary>A proper name of a place, which the policy preserves.</summary>
    public static DecidedOccurrence Preserved(int start, int end) => Decided(OccurrenceKinds.Location, "PROPN", start, end);

    public static Sanitizer Sanitizer(params ISanitizationStep[] steps) => new(new SanitizationSteps(steps), Boundaries);

    public static Sanitizer OfDestinations() => Sanitizer(new DestinationsStep());

    public static SanitizedText Sanitized(Sanitizer sanitizer, string raw, params DecidedOccurrence[] decided) =>
        Assert.IsType<SanitizedText>(sanitizer.Sanitize(raw, decided).Sanitized);

    public static SanitizedText Sanitized(string raw, params DecidedOccurrence[] decided) => Sanitized(OfDestinations(), raw, decided);

    public static string Cut(string text, int start, int end) => Boundaries.Span(text, start, end);
}
