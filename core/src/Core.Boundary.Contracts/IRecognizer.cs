namespace Core.Boundary.Contracts;

/// <summary>The port through which the core asks for the occurrences in a text.</summary>
public interface IRecognizer
{
    /// <summary>Asks once; an unavailable service is an outcome, not an exception.</summary>
    Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken);
}
