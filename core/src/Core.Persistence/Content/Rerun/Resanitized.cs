using Core.Persistence.Content.Writing;

namespace Core.Persistence.Content.Rerun;

/// <summary>What the rerun of an item produced, and whether the text is, ordinal, the one it holds; the text and the trail are sealed on their way.</summary>
/// <param name="Text">The sanitized text.</param>
/// <param name="Version">The version of the steps that produced it.</param>
/// <param name="Steps">The steps, one name@version per line.</param>
/// <param name="Trail">The trail back to the raw text.</param>
/// <param name="Counts">The edits of each step, in order.</param>
/// <param name="Untreated">What each step left untreated, in order.</param>
/// <param name="Identical">Whether the text is the one the item holds.</param>
public sealed record Resanitized(string Text, string Version, string Steps, string Trail, IReadOnlyList<int> Counts, IReadOnlyList<int> Untreated, bool Identical)
{
    /// <summary>
    /// The markers the item holds after the rerun, the ones it held already among them, drawn again for none; null when the rerun does not pseudonymize. The markers the text
    /// no longer holds leave the item and stay held in the installation.
    /// </summary>
    public IReadOnlyList<HeldMarker>? Markers { get; init; }

    /// <summary>The version, the counts and whether it is identical; never the text nor the trail.</summary>
    public override string ToString() => $"Resanitized {{ Version = {Version}, Counts = {string.Join(", ", Counts)}, Identical = {Identical} }}";
}
