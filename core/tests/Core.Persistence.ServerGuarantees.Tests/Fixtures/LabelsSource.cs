using Core.Platform.Keys;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>A source of the master key for the tests of the labels: a key of the right size with the labels given, or none at all.</summary>
internal sealed class LabelsSource : IMasterKeySource
{
    private readonly IReadOnlyList<string>? labels;

    private LabelsSource(IReadOnlyList<string>? labels)
    {
        this.labels = labels;
    }

    public string Origin => "fonte de teste";

    public static LabelsSource Providing(params IReadOnlyList<string> labels) => new(labels);

    public static LabelsSource WithoutKey() => new(labels: null);

    public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(labels is null
            ? MasterKeyReading.Absent("a chave não foi fornecida")
            : MasterKeyReading.FromBytes(new byte[MasterKey.Length], DerivationLabels.Of(labels)));
}
