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
