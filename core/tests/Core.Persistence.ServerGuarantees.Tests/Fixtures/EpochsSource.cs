using Core.Platform.Keys;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>A source whose key reads and whose epochs are the numbers given, each with a secret of its own; or one whose key was not read.</summary>
internal sealed class EpochsSource : IMasterKeySource
{
    private readonly IReadOnlyList<int>? epochs;

    private EpochsSource(IReadOnlyList<int>? epochs)
    {
        this.epochs = epochs;
    }

    public string Origin => "fonte de teste";

    public static EpochsSource Providing(params IReadOnlyList<int> epochs) => new(epochs);

    public static EpochsSource WithoutKey() => new(epochs: null);

    public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(epochs is null
            ? MasterKeyReading.Absent("a chave não foi fornecida")
            : MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.None, CustodyEpochs.Of(epochs.ToDictionary(number => number, Secret)), string.Empty));

    private static byte[] Secret(int number) => [.. Enumerable.Repeat((byte)number, MasterKey.Length)];
}
