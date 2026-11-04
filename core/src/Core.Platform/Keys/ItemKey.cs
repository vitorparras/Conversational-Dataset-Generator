using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>The key of one row of protected content: random, held only in memory, shown by no text nor serialization and zeroed when released.</summary>
public sealed class ItemKey : IDisposable
{
    /// <summary>The size every key of a row has, in bytes.</summary>
    public static int Length { get; } = 32;

    private readonly byte[] bytes = GC.AllocateArray<byte>(Length, pinned: true);
    private bool released;

    internal ItemKey(ReadOnlySpan<byte> value)
    {
        value.CopyTo(bytes);
    }

    /// <summary>The key's bytes, for the cipher that needs them; a released key has none to give.</summary>
    public ReadOnlySpan<byte> AsSpan() => released ? throw new ObjectDisposedException(nameof(ItemKey)) : bytes;

    /// <summary>Zeroes the key; it cannot be read afterwards.</summary>
    public void Dispose()
    {
        released = true;
        CryptographicOperations.ZeroMemory(bytes);
    }

    /// <summary>Names the type and nothing of the value.</summary>
    public override string ToString() => nameof(ItemKey);
}
