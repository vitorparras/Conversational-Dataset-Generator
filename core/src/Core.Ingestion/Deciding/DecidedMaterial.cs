using Core.Boundary.Contracts;

namespace Core.Ingestion.Deciding;

/// <summary>
/// An item whose every occurrence has a destination: the only shape in which material reaches the writing. It is built by <see cref="Decide" /> alone, from one analysis for each text, and an
/// item whose text was not analysed does not become one, so that an empty list and a service that did not answer never arrive alike.
/// </summary>
public sealed record DecidedMaterial
{
    private readonly IReadOnlyList<IReadOnlyList<DecidedOccurrence>> byText;

    private DecidedMaterial(Material material, IReadOnlyList<IReadOnlyList<DecidedOccurrence>> byText)
    {
        Material = material;
        this.byText = byText;
    }

    /// <summary>The item, as it passed the conference of its form.</summary>
    public Material Material { get; }

    /// <summary>The occurrences of a text and the destination of each, in the order the service gave them; empty when the text was analysed and nothing was found.</summary>
    /// <param name="text">The position of the text among <see cref="MaterialTexts.Of" />.</param>
    public IReadOnlyList<DecidedOccurrence> Of(int text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(text, byText.Count);
        return byText[text];
    }

    /// <summary>Only how many texts and how many occurrences: never a kind, an offset or a word of the material.</summary>
    public override string ToString() => $"DecidedMaterial {{ Texts = {byText.Count}, Occurrences = {byText.Sum(occurrences => occurrences.Count)} }}";

    /// <summary>The destinations of every occurrence of the item, or the reason the service gave no analysis for one of its texts.</summary>
    /// <param name="material">An item that passed the conference of its form.</param>
    /// <param name="analyses">What the recognizer answered for each text of <see cref="MaterialTexts.Of" />, in the same order.</param>
    /// <exception cref="ArgumentException">The answers are not one for each text of the item.</exception>
    public static DestinationResult Decide(Material material, IReadOnlyList<RecognitionOutcome> analyses)
    {
        ArgumentNullException.ThrowIfNull(analyses);
        if (analyses.Count != MaterialTexts.Of(material).Count)
        {
            throw new ArgumentException("There must be one analysis for each text of the item.", nameof(analyses));
        }

        var missing = analyses.FirstOrDefault(analysis => analysis.Response is null);
        return missing is null
            ? DestinationResult.Of(new DecidedMaterial(material, DecidedTexts(analyses)))
            : DestinationResult.Unavailable(missing.Reason!.Value);
    }

    private static List<IReadOnlyList<DecidedOccurrence>> DecidedTexts(IEnumerable<RecognitionOutcome> analyses)
    {
        var texts = analyses.Select(Decided).ToList();
        return texts;
    }

    private static IReadOnlyList<DecidedOccurrence> Decided(RecognitionOutcome analysis)
    {
        var decided = analysis.Response!.Occurrences.Select(occurrence => new DecidedOccurrence(occurrence, DestinationPolicy.Of(occurrence))).ToList();
        return decided;
    }
}
