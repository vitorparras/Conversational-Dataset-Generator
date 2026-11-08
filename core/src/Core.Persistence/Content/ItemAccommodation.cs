namespace Core.Persistence.Content;

/// <summary>
/// What an item declares when its material was accommodated into a form that is not exactly its own: the form it received and the codes of what was lost. It cannot be built without
/// a loss, so the writer has no way to ask for an accommodated item with nothing declared, and the database refuses it as well. Nothing here is text of the material.
/// </summary>
public sealed record ItemAccommodation
{
    private const int MostCharacters = 63;

    /// <summary>The declaration, as the database keeps it.</summary>
    /// <param name="form">The code of the form received.</param>
    /// <param name="losses">The codes of the losses: at least one.</param>
    public ItemAccommodation(string form, IReadOnlyCollection<string> losses)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(losses);
        if (!IsCode(form))
        {
            throw new ArgumentException("The form is a code in lowercase, digits and underscores.", nameof(form));
        }

        if (losses.Count == 0)
        {
            throw new ArgumentException("An item accommodated declares at least one loss.", nameof(losses));
        }

        if (!losses.All(IsCode))
        {
            throw new ArgumentException("A loss is a code in lowercase, digits and underscores.", nameof(losses));
        }

        Form = form;
        Losses = losses.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>The code of the form the item received.</summary>
    public string Form { get; }

    /// <summary>The codes of what that form does not keep.</summary>
    public IReadOnlyList<string> Losses { get; }

    private static bool IsCode(string? text) =>
        text is { Length: > 0 and <= MostCharacters } && text[0] is >= 'a' and <= 'z' && text.All(character => character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
}
