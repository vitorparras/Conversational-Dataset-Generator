namespace Core.Boundary.Contracts;

/// <summary>One occurrence in one text of an item: a half-open span of code points in that text alone, and the coreference group it belongs to.</summary>
/// <param name="TextIndex">The position of the text among the texts of the request.</param>
/// <param name="Start">The code point offset in that text where the occurrence starts.</param>
/// <param name="End">The code point offset in that text just past the occurrence.</param>
/// <param name="Kind">What the occurrence is, as the recognizer names it.</param>
/// <param name="PartOfSpeech">The part of speech of the head of the occurrence.</param>
/// <param name="SyntacticRole">The syntactic role of the head of the occurrence.</param>
/// <param name="Group">The coreference group: every mention of the same entity in the item has the same one.</param>
public sealed record GroupedOccurrence(int TextIndex, int Start, int End, string Kind, string PartOfSpeech, string SyntacticRole, int Group)
{
    /// <summary>The text index, kind, grammar and group; never the offsets, which with the text rebuild what was found.</summary>
    public override string ToString() =>
        $"GroupedOccurrence {{ TextIndex = {TextIndex}, Kind = {Kind}, PartOfSpeech = {PartOfSpeech}, SyntacticRole = {SyntacticRole}, Group = {Group} }}";
}
