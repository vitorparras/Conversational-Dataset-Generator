namespace Core.Boundary.Contracts;

/// <summary>One occurrence in the text sent: a half-open span measured in code points, never in UTF-16 units, with no confidence.</summary>
/// <param name="Start">The code point offset where the occurrence starts.</param>
/// <param name="End">The code point offset just past the occurrence.</param>
/// <param name="Kind">What the occurrence is, as the recognizer names it.</param>
/// <param name="PartOfSpeech">The part of speech of the head of the occurrence.</param>
/// <param name="SyntacticRole">The syntactic role of the head of the occurrence.</param>
public sealed record Occurrence(int Start, int End, string Kind, string PartOfSpeech, string SyntacticRole)
{
    /// <summary>The kind, part of speech and syntactic role; never the offsets, which with the text rebuild what was found.</summary>
    public override string ToString() =>
        $"Occurrence {{ Kind = {Kind}, PartOfSpeech = {PartOfSpeech}, SyntacticRole = {SyntacticRole} }}";
}
