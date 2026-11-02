namespace Core.Persistence.Content;

/// <summary>The key of a row as the row keeps it: wrapped, with the number of the epoch whose secret wrapped it (decision 0049).</summary>
/// <param name="Epoch">The epoch whose secret wrapped the key.</param>
/// <param name="Wrapped">The key wrapped.</param>
public sealed record ContentKey(int Epoch, ReadOnlyMemory<byte> Wrapped);
