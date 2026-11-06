namespace Core.Ingestion;

/// <summary>
/// An item of material that passed the conference, and the form it has: exactly one of <see cref="Conversation" />, <see cref="Comment" />, <see cref="Document" /> and
/// <see cref="LabeledSequence" />. Only this assembly can derive from it, so the type is the form and no item answers for none or for two.
/// </summary>
public abstract class Material
{
    private protected Material()
    {
    }

    /// <summary>The form of the item, which is the form of its type.</summary>
    public abstract MaterialForm Form { get; }
}
