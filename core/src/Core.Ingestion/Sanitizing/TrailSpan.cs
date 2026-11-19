namespace Core.Ingestion.Sanitizing;

/// <summary>A span translated through the trail: either it still exists, with its offsets in the other text, or it no longer exists, and then it has no offsets to read.</summary>
public sealed class TrailSpan
{
    private readonly (int Start, int End)? offsets;

    private TrailSpan((int Start, int End)? offsets)
    {
        this.offsets = offsets;
    }

    /// <summary>The span touched something a step removed, and no position answers for it.</summary>
    public static TrailSpan Gone { get; } = new(null);

    /// <summary>Whether the span survived every step.</summary>
    public bool Exists => offsets.HasValue;

    /// <summary>The code point offset where the span starts in the other text; reading it from a span that no longer exists is a defect.</summary>
    public int Start => offsets?.Start ?? throw new InvalidOperationException("The span no longer exists.");

    /// <summary>The code point offset just past the span in the other text; reading it from a span that no longer exists is a defect.</summary>
    public int End => offsets?.End ?? throw new InvalidOperationException("The span no longer exists.");

    /// <summary>Whether it exists; never the offsets.</summary>
    public override string ToString() => $"TrailSpan {{ Exists = {Exists} }}";

    /// <summary>The span, where it now lies.</summary>
    internal static TrailSpan At(int start, int end) => new((start, end));
}
