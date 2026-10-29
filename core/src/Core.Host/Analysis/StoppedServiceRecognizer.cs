using Core.Boundary.Contracts;

namespace Core.Host.Analysis;

/// <summary>
/// The recognizer of a language service that is not running: it refuses every request as out of reach, without opening
/// a connection, so that neither the secret nor the text goes to whatever listens on a port the service once held.
/// </summary>
/// <param name="logger">Where the refusal is written, by the reference of the request only.</param>
internal sealed class StoppedServiceRecognizer(ILogger<StoppedServiceRecognizer> logger) : IRecognizer
{
    /// <inheritdoc />
    public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        logger.NotSent(request.Reference);
        return Task.FromResult(RecognitionOutcome.Unavailable(UnavailabilityReason.ConnectionRefused));
    }
}
