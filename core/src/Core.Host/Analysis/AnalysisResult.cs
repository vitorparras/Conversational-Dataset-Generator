namespace Core.Host.Analysis;

/// <summary>Either the concluded analysis or the refusal; never both, never neither.</summary>
internal sealed class AnalysisResult
{
    private AnalysisResult(ConcludedAnalysis? concluded, RefusedAnalysis? refused)
    {
        Concluded = concluded;
        Refused = refused;
    }

    /// <summary>The analysis; null when it did not conclude.</summary>
    internal ConcludedAnalysis? Concluded { get; }

    /// <summary>Why there is no analysis; null when it concluded.</summary>
    internal RefusedAnalysis? Refused { get; }

    internal static AnalysisResult Of(ConcludedAnalysis concluded) => new(concluded, refused: null);

    internal static AnalysisResult Of(RefusedAnalysis refused) => new(concluded: null, refused);
}
