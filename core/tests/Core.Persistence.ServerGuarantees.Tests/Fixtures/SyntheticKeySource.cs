using Core.Persistence.Tests.Content;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Fixtures;

/// <summary>The source that gives the synthetic key the tests of the content seal with, and the labels given, so that what the writer sealed is what the conference of the sample opens.</summary>
internal sealed class SyntheticKeySource : IMasterKeySource
{
    private readonly IReadOnlyList<string> labels;

    internal SyntheticKeySource(IReadOnlyList<string> labels)
    {
        this.labels = labels;
    }

    public string Origin => "fonte de teste";

    public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(MasterKeyReading.FromBytes(SyntheticCustody.KeyBytes(), DerivationLabels.Of(labels)));
}
