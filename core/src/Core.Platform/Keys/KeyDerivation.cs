namespace Core.Platform.Keys;

/// <summary>Derives the key of a label from the master key, and only of a label that was loaded with it.</summary>
public sealed class KeyDerivation
{
    private readonly MasterKey master;
    private readonly DerivationLabels loaded;

    /// <summary>The derivation reads the master key it was given, which stays with whoever owns it.</summary>
    public KeyDerivation(MasterKey master, DerivationLabels loaded)
    {
        ArgumentNullException.ThrowIfNull(master);
        ArgumentNullException.ThrowIfNull(loaded);
        this.master = master;
        this.loaded = loaded;
    }

    /// <summary>The key of the label, always the same for the same master key and label; a label not loaded is refused by name.</summary>
    public DerivedKey Derive(string label)
    {
        ArgumentNullException.ThrowIfNull(label);
        return loaded.Holds(label) ? new DerivedKey(master.AsSpan(), label) : throw new InvalidOperationException($"The derivation label '{label}' is not among the loaded ones.");
    }
}
