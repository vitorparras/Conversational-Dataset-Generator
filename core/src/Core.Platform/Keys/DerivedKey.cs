using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>The key of one derivation label: held only in memory, shown by no text nor serialization, and zeroed when released.</summary>
public sealed class DerivedKey : IDisposable
{
    /// <summary>The size every derived key has, in bytes.</summary>
    public static int Length { get; } = 32;

    private readonly byte[] bytes = GC.AllocateArray<byte>(Length, pinned: true);
    private bool released;

    internal DerivedKey(ReadOnlySpan<byte> master, string label)
    {
        Hkdf.Derive(master, System.Text.Encoding.UTF8.GetBytes(label), bytes);
    }

    /// <summary>The key's bytes, for the cipher that needs them; a released key has none to give.</summary>
    public ReadOnlySpan<byte> AsSpan() => released ? throw new ObjectDisposedException(nameof(DerivedKey)) : bytes;

    internal ReadOnlySpan<byte> Held => bytes;

    /// <summary>Zeroes the key; it cannot be read afterwards.</summary>
    public void Dispose()
    {
        released = true;
        CryptographicOperations.ZeroMemory(bytes);
    }

    /// <summary>Names the type and nothing of the value.</summary>
    public override string ToString() => nameof(DerivedKey);
}
