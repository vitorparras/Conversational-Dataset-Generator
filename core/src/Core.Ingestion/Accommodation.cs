namespace Core.Ingestion;

/// <summary>
/// What an item declares when its material was accommodated into a form that is not exactly its own: the form it received and what was lost on the way. There is no way to build one
/// without at least one loss, so that an accommodated item with nothing declared does not exist in the type, as it does not in the database.
/// </summary>
public sealed record Accommodation
{
    /// <summary>The declaration of an accommodation.</summary>
    /// <param name="received">The form the material was accommodated into.</param>
    /// <param name="losses">What that form does not keep: at least one, each from the closed set, none twice.</param>
    public Accommodation(MaterialForm received, IReadOnlyCollection<Loss> losses)
    {
        ArgumentNullException.ThrowIfNull(losses);
        if (!Enum.IsDefined(received))
        {
            throw new ArgumentOutOfRangeException(nameof(received), received, "The form is not one of the four.");
        }

        if (losses.Count == 0)
        {
            throw new ArgumentException("An accommodation declares at least one loss.", nameof(losses));
        }

        if (losses.Any(loss => !Enum.IsDefined(loss)))
        {
            throw new ArgumentException("A loss is not one of the set.", nameof(losses));
        }

        var ordered = losses.Distinct().Order().ToList();
        var codes = ordered.Select(loss => LossCodes.Code(loss)).ToList();
        var words = ordered.Select(loss => LossCodes.Words(loss)).ToList();
        Received = received;
        Losses = ordered;
        Codes = codes;
        Words = words;
    }

    /// <summary>The form the material was accommodated into.</summary>
    public MaterialForm Received { get; }

    /// <summary>What that form does not keep, in the order of the set.</summary>
    public IReadOnlyList<Loss> Losses { get; }

    /// <summary>The code of the form received, as it is stored.</summary>
    public string FormCode => LossCodes.Code(Received);

    /// <summary>The codes of the losses, as they are stored, in the order of the set.</summary>
    public IReadOnlyList<string> Codes { get; }

    /// <summary>The text that describes each loss, taken from the codes.</summary>
    public IReadOnlyList<string> Words { get; }
}
