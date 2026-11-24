using Core.Boundary.Contracts;
using Core.Boundary.Text;

namespace Core.Boundary.Http;

/// <summary>
/// What refuses an answer for a whole item once its shape fits the contract: an occurrence in a text the item does not
/// have, or outside the text it names; a kind outside the closed list; an author without exactly one group.
/// </summary>
internal static class ItemAnswerCheck
{
    /// <summary>The first problem, the occurrences before the authors; null when every value fits the request.</summary>
    /// <param name="request">The request the answer is for; its texts are what the offsets are checked against.</param>
    /// <param name="response">The answer, already read as the contract declares it.</param>
    internal static ValidationProblem? FirstProblem(ItemAnalysisRequest request, ItemAnalysisResponse response) =>
        FirstOccurrenceProblem(request.Texts, response.Occurrences)
            ?? FirstKindProblem(response.Occurrences)
            ?? AuthorGroupProblem(request.Texts, response.AuthorGroups);

    /// <summary>The length of each text in code points comes from the converter, never from counting here.</summary>
    private static ValidationProblem? FirstOccurrenceProblem(IEnumerable<ItemText> texts, IEnumerable<GroupedOccurrence> occurrences)
    {
        int[] lengths = [.. texts.Select(text => OffsetConverter.ToCodePointOffset(text.Text, text.Text.Length))];
        return occurrences
            .Select(occurrence => occurrence.TextIndex < 0 || occurrence.TextIndex >= lengths.Length
                ? ValidationProblem.TextOutsideItem
                : SpanProblem(occurrence, lengths[occurrence.TextIndex]))
            .FirstOrDefault(problem => problem is not null);
    }

    private static ValidationProblem? SpanProblem(GroupedOccurrence occurrence, int textLength)
    {
        if (occurrence.End < occurrence.Start)
        {
            return ValidationProblem.EndBeforeStart;
        }

        return occurrence.Start < 0 || occurrence.End > textLength ? ValidationProblem.OffsetOutsideText : null;
    }

    private static ValidationProblem? FirstKindProblem(IEnumerable<GroupedOccurrence> occurrences)
    {
        if (occurrences.Any(occurrence => !OccurrenceKinds.IsKnown(occurrence.Kind)))
        {
            return ValidationProblem.KindOutsideVocabulary;
        }

        return null;
    }

    /// <summary>One group for each distinct author named in the request, none missing, none repeated and none for a name it did not send.</summary>
    private static ValidationProblem? AuthorGroupProblem(IEnumerable<ItemText> texts, IReadOnlyCollection<AuthorGroup> groups)
    {
        var named = texts.Select(text => text.Author).OfType<string>().ToHashSet(StringComparer.Ordinal);
        return groups.Count == named.Count && named.SetEquals(groups.Select(group => group.Author)) ? null : ValidationProblem.AuthorWithoutOneGroup;
    }
}
