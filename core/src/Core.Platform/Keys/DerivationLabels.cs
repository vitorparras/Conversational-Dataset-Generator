namespace Core.Platform.Keys;

/// <summary>The labels of derivation already used, which travel with the master key; the set only grows, because dropping a label makes the data under it unreadable.</summary>
public sealed class DerivationLabels
{
    private readonly SortedSet<string> labels;

    private DerivationLabels(IEnumerable<string> labels)
    {
        this.labels = new SortedSet<string>(labels, StringComparer.Ordinal);
    }

    /// <summary>The set that holds no label yet.</summary>
    public static DerivationLabels None { get; } = new([]);

    /// <summary>The labels in order.</summary>
    public IReadOnlyList<string> All => [.. labels];

    /// <summary>The set of the labels given; a label that is empty or has spaces around it is a defect of whoever read it.</summary>
    public static DerivationLabels Of(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var given = labels.ToList();
        given.ForEach(Validate);
        return new DerivationLabels(given);
    }

    /// <summary>This set with one label more; the set itself does not change.</summary>
    public DerivationLabels With(string label)
    {
        Validate(label);
        return new DerivationLabels(labels.Append(label));
    }

    /// <summary>The labels of those given that this set does not hold, in order and once each.</summary>
    public IReadOnlyList<string> Lacking(IEnumerable<string> used)
    {
        ArgumentNullException.ThrowIfNull(used);
        return [.. used.Where(label => !labels.Contains(label)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static void Validate(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (label != label.Trim())
        {
            throw new ArgumentException("A derivation label has no space around it.", nameof(label));
        }
    }
}
