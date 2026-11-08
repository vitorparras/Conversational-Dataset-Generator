namespace Core.Ingestion;

/// <summary>
/// The accommodation of a material into a form it does not fit exactly: what the form does not keep of each unit, which the unit carried. A unit that fits the form exactly declares nothing,
/// and accommodating is never the default: it is what a material gets only when it brings more than the form keeps. The form keeps the author and the instant of a conversation and of a comment,
/// the replies of a conversation, the passages of a labeled sequence and the text of every form; a comment is one manifestation, and the thread of several is lost.
/// </summary>
public static class Accommodating
{
    /// <summary>For each unit of the material, in order, the accommodation into the form, or null when the unit fits the form exactly and nothing is lost.</summary>
    /// <param name="read">The material that was read.</param>
    /// <param name="form">The form it is accommodated into.</param>
    public static IReadOnlyList<Accommodation?> Of(ReadMaterial read, MaterialForm form)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (!Enum.IsDefined(form))
        {
            throw new ArgumentOutOfRangeException(nameof(form), form, "The form is not one of the four.");
        }

        var thread = form == MaterialForm.Comment && read.Units.Count > 1;
        return read.Units.Select(unit => Declared(LossesOf(unit, form, thread), form)).ToList();
    }

    private static Accommodation? Declared(List<Loss> losses, MaterialForm form) => losses.Count == 0 ? null : new Accommodation(form, losses);

    private static List<Loss> LossesOf(ReadUnit unit, MaterialForm form, bool thread)
    {
        var withoutAuthor = form is MaterialForm.Document or MaterialForm.LabeledSequence;
        var losses = new List<Loss>();
        AddWhen(losses, Loss.Author, unit.Author is not null && withoutAuthor);
        AddWhen(losses, Loss.Instant, unit.Instant is not null && withoutAuthor);
        AddWhen(losses, Loss.RepliesTo, unit.RepliesTo is not null && form != MaterialForm.Conversation);
        AddWhen(losses, Loss.Passages, unit.Passages.Count > 0 && form != MaterialForm.LabeledSequence);
        AddWhen(losses, Loss.Thread, thread);
        return losses;
    }

    private static void AddWhen(List<Loss> losses, Loss loss, bool lost)
    {
        if (lost)
        {
            losses.Add(loss);
        }
    }
}
