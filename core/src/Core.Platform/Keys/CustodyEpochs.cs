using System.Security.Cryptography;

namespace Core.Platform.Keys;

/// <summary>
/// The epochs the third custody file holds (decision 0049): each a number and a random secret of 32 bytes, held only in memory, shown by no text nor serialization and zeroed when
/// released. The newest wraps the key of each item written; an older one stays only while the keys wrapped under it are wrapped again, and is then taken out of the file.
/// </summary>
public sealed class CustodyEpochs : IDisposable
{
    private readonly SortedDictionary<int, byte[]> secrets;
    private bool released;

    private CustodyEpochs()
    {
        secrets = new SortedDictionary<int, byte[]>(Comparer<int>.Default);
    }

    private CustodyEpochs(IEnumerable<KeyValuePair<int, byte[]>> given)
    {
        secrets = new SortedDictionary<int, byte[]>(given.ToDictionary(pair => pair.Key, pair => Pinned(pair.Value)));
    }

    /// <summary>The set that holds no epoch, which an installation that has not read the file has.</summary>
    public static CustodyEpochs None { get; } = new();

    /// <summary>The numbers of the epochs, in order.</summary>
    public IEnumerable<int> Numbers() => secrets.Keys.ToArray().AsEnumerable();

    /// <summary>The newest epoch, the one that wraps what is written now; there is none in a set that holds none.</summary>
    public int Newest => secrets.Count > 0 ? secrets.Keys.Max() : throw new InvalidOperationException("No epoch is loaded.");

    /// <summary>The epochs of the numbers and secrets given, whose secrets are zeroed here so that no other copy stays behind;
    /// a number below 1 or a secret of another size is a defect of the caller.</summary>
    public static CustodyEpochs Of(IReadOnlyDictionary<int, byte[]> given)
    {
        ArgumentNullException.ThrowIfNull(given);
        try
        {
            foreach (var (number, secret) in given)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
                ArgumentOutOfRangeException.ThrowIfNotEqual(secret.Length, MasterKey.Length);
            }

            return new CustodyEpochs(given);
        }
        finally
        {
            foreach (var secret in given.Values)
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }
    }

    private static byte[] Pinned(byte[] secret)
    {
        var copy = GC.AllocateArray<byte>(MasterKey.Length, pinned: true);
        secret.CopyTo(copy, 0);
        return copy;
    }

    /// <summary>Whether the epoch is loaded.</summary>
    public bool Holds(int epoch) => secrets.ContainsKey(epoch);

    /// <summary>The numbers among those given that are not loaded, in order and once each.</summary>
    public IEnumerable<int> Lacking(IEnumerable<int> used)
    {
        ArgumentNullException.ThrowIfNull(used);
        return used.Where(epoch => !secrets.ContainsKey(epoch)).Distinct().Order();
    }

    /// <summary>The secret of the epoch, for the wrapping that needs it; an epoch not loaded is refused by number, and a released set has none to give.</summary>
    internal ReadOnlySpan<byte> SecretOf(int epoch)
    {
        ObjectDisposedException.ThrowIf(released, this);
        return secrets.TryGetValue(epoch, out var secret) ? secret : throw new InvalidOperationException($"The epoch {epoch} is not among the loaded ones.");
    }

    /// <summary>Zeroes every secret; none can be read afterwards.</summary>
    public void Dispose()
    {
        released = true;
        foreach (var secret in secrets.Values)
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>Names the type and nothing of the secrets.</summary>
    public override string ToString() => nameof(CustodyEpochs);
}
