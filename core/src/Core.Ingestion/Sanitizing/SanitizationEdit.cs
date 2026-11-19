namespace Core.Ingestion.Sanitizing;

/// <summary>A span a step replaces with a marker: half-open, in code points of the text the step received.</summary>
/// <param name="Start">The code point offset where the replaced span starts.</param>
/// <param name="End">The code point offset just past the replaced span.</param>
/// <param name="Marker">What is written in its place.</param>
public sealed record SanitizationEdit(int Start, int End, SanitizationMarker Marker)
{
    /// <summary>The marker; never the offsets, which with the raw text rebuild what was removed.</summary>
    public override string ToString() => $"SanitizationEdit {{ Marker = {Marker} }}";
}
