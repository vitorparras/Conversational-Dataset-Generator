using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The test adapter of the port: gives the key, the labels or the failure it was built with, and every read hands over a reading of its own.</summary>
internal sealed class FakeMasterKeySource : IMasterKeySource
{
    private readonly Func<MasterKeyReading> read;

    private FakeMasterKeySource(Func<MasterKeyReading> read)
    {
        this.read = read;
    }

    public string Origin => "fonte de teste";

    /// <summary>A source that gives a copy of the key and the labels.</summary>
    public static FakeMasterKeySource Providing(byte[] key, params IEnumerable<string> labels) =>
        new(() => MasterKeyReading.FromBytes([.. key], DerivationLabels.Of(labels)));

    /// <summary>A source with nothing to give; <paramref name="missing"/> says what.</summary>
    public static FakeMasterKeySource Lacking(string missing) => new(() => MasterKeyReading.Absent(missing));

    /// <summary>A source whose content cannot be read as a key.</summary>
    public static FakeMasterKeySource Unreadable(string found) => new(() => MasterKeyReading.Unreadable(found));

    public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(read());
}
