namespace Core.Ingestion;

/// <summary>What the minimal rules share: the units that fail a test, as findings, and the first refusal among the tests of a form.</summary>
internal static class RuleChecks
{
    /// <summary>The unit that fails, one finding for each, in the order of the units.</summary>
    internal static List<Finding> Failing(ReadMaterial read, MaterialField field, Func<ReadUnit, bool> fails)
    {
        var found = new List<Finding>();
        for (var position = 0; position < read.Units.Count; position++)
        {
            if (fails(read.Units[position]))
            {
                found.Add(new Finding(position, field));
            }
        }

        return found;
    }

    /// <summary>What <paramref name="make" /> builds from each unit, in the order of the units.</summary>
    internal static List<TResult> Each<TResult>(ReadMaterial read, Func<ReadUnit, TResult> make)
    {
        var made = new List<TResult>();
        foreach (var unit in read.Units)
        {
            made.Add(make(unit));
        }

        return made;
    }

    /// <summary>The refusal of the findings under the code, or none when there is no finding.</summary>
    internal static ConferenceResult? Refusal(MaterialForm form, RefusalCode code, IReadOnlyList<Finding> found) =>
        found.Count == 0 ? null : ConferenceResult.Refuse(new Refusal(form, code, null, found));

    /// <summary>Whether the text has no character but blanks.</summary>
    internal static bool Blank(string? text) => string.IsNullOrWhiteSpace(text);
}
