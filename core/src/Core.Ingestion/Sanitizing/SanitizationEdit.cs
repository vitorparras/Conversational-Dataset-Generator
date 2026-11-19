namespace Core.Ingestion.Sanitizing;

/// <summary>
/// A span a step replaces, half-open, in code points of the text the step received: with a typed marker, when something is removed, or with the text the step writes, when it
/// repairs or takes away what nobody wrote. Exactly one of the two is given.
/// </summary>
/// <param name="Start">The code point offset where the replaced span starts.</param>
/// <param name="End">The code point offset just past the replaced span.</param>
/// <param name="Marker">The marker written in its place; null when the step writes text instead.</param>
/// <param name="Replacement">The text written in its place, empty when the span is taken away; null when a marker is written.</param>
public sealed record SanitizationEdit(int Start, int End, SanitizationMarker? Marker, string? Replacement = null)
{
    /// <summary>A span whose place takes the text given, empty to take the span away.</summary>
    /// <param name="start">The code point offset where the replaced span starts.</param>
    /// <param name="end">The code point offset just past the replaced span.</param>
    /// <param name="replacement">The text written in its place.</param>
    public static SanitizationEdit Rewriting(int start, int end, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        return new(start, end, null, replacement);
    }

    /// <summary>Whether exactly one of the marker and the replacement is given.</summary>
    internal bool IsWellFormed => Marker.HasValue != (Replacement is not null);

    /// <summary>What the sanitized text holds in place of the span.</summary>
    internal string Written => Marker is { } marker ? SanitizationMarkers.Text(marker) : Replacement ?? throw new InvalidOperationException("The edit writes nothing.");

    /// <summary>How many code points the edit writes.</summary>
    internal int Width => Written.EnumerateRunes().Count();

    /// <summary>The marker; never the offsets nor the replacement, which with the raw text rebuild what was there.</summary>
    public override string ToString() => $"SanitizationEdit {{ Marker = {Marker} }}";
}
