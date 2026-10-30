using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>The installation's master key: held only in memory, shown by no text nor serialization, and zeroed when released.</summary>
public sealed class MasterKey : IDisposable
{
    /// <summary>The size every master key has, in bytes.</summary>
    public static int Length { get; } = 32;

    private readonly byte[] bytes;
    private bool released;

    internal MasterKey(ReadOnlySpan<byte> value)
    {
        if (value.Length != Length)
        {
            throw new ArgumentException($"A master key has {Length} bytes.", nameof(value));
        }

        bytes = value.ToArray();
    }

    /// <summary>The key's bytes, for the derivation that needs them; a released key has none to give.</summary>
    public ReadOnlySpan<byte> AsSpan() => released ? throw new ObjectDisposedException(nameof(MasterKey)) : bytes;

    internal ReadOnlySpan<byte> Held => bytes;

    /// <summary>Zeroes the key; it cannot be read afterwards.</summary>
    public void Dispose()
    {
        released = true;
        CryptographicOperations.ZeroMemory(bytes);
    }

    /// <summary>Names the type and nothing of the value.</summary>
    public override string ToString() => nameof(MasterKey);
}
