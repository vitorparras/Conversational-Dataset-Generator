namespace Core.Boundary.Contracts;

/// <summary>The port through which the core asks for the occurrences in a text, or in a whole item with their coreference groups.</summary>
public interface IRecognizer
{
    /// <summary>Asks once; an unavailable service is an outcome, not an exception.</summary>
    Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken);

    /// <summary>Asks once for every text of an item together, so that the mentions of one entity across its texts share a group; an unavailable service is an outcome, not an exception.</summary>
    Task<ItemRecognitionOutcome> RecognizeItemAsync(ItemAnalysisRequest request, CancellationToken cancellationToken);
}
