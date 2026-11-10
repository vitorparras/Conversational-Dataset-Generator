using System.Security.Cryptography;
using System.Text;

namespace Core.Platform.Keys;

/// <summary>Derives the key of a label from the master key, and only of a label that was loaded with it; and the keyed digest of a text under a key of a purpose of its own.</summary>
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

    /// <summary>
    /// The keyed digest of a text under the key the master key gives for a purpose, in lowercase hexadecimal: always the same for the same master key, purpose and text, and not undone without
    /// the master key. The purpose is a name of its own and never the text of a derivation label, so that no digest shares a key with a content key.
    /// </summary>
    /// <param name="purpose">The name of what the digest is for, with the domain in it when it is per domain.</param>
    /// <param name="text">The text to digest.</param>
    public string Digest(string purpose, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        if (DerivationLabel.TryParse(purpose, out _))
        {
            throw new ArgumentException("A purpose of a digest is never the text of a derivation label.", nameof(purpose));
        }

        ArgumentNullException.ThrowIfNull(text);
        using var key = new DerivedKey(master.AsSpan(), purpose);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key.AsSpan(), Encoding.UTF8.GetBytes(text)));
    }
}
