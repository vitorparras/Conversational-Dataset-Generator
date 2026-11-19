namespace Core.Ingestion.Deciding;

/// <summary>The texts of an item, in the one order in which the occurrences of each are asked for and kept.</summary>
public static class MaterialTexts
{
    /// <summary>The texts of the item: the messages in the order of the conversation, the one text of the comment, the sections of the document and the texts of the sequence.</summary>
    /// <param name="material">An item that passed the conference of its form.</param>
    public static IReadOnlyList<string> Of(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return material switch
        {
            Conversation conversation => OfConversation(conversation),
            Comment comment => OfComment(comment),
            Document document => document.Sections,
            LabeledSequence sequence => OfSequence(sequence),
            _ => throw new ArgumentOutOfRangeException(nameof(material), material.Form, "The form is not one of the four."),
        };
    }

    /// <summary>For each unit of the file, in the order of the file, the position of its text among the texts of the item: only the conversation puts its messages in another order.</summary>
    /// <param name="material">The item the units were accepted as.</param>
    /// <param name="read">The units as they were read, in the order of the file.</param>
    /// <exception cref="ArgumentException">The units read are not one for each text of the item.</exception>
    public static IReadOnlyList<int> PositionsOf(Material material, ReadMaterial read)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(read);
        if (Of(material).Count != read.Units.Count)
        {
            throw new ArgumentException("The units read are not one for each text of the item.", nameof(read));
        }

        return material is Conversation ? ConversationOrder.Placed(ConversationOrder.Sequence(read)) : [.. Enumerable.Range(0, read.Units.Count)];
    }

    private static List<string> OfConversation(Conversation conversation)
    {
        var texts = conversation.Messages.Select(message => message.Text).ToList();
        return texts;
    }

    private static List<string> OfComment(Comment comment)
    {
        var texts = Enumerable.Repeat(comment.Text, 1).ToList();
        return texts;
    }

    private static List<string> OfSequence(LabeledSequence sequence)
    {
        var texts = sequence.Texts.Select(labeled => labeled.Text).ToList();
        return texts;
    }
}
