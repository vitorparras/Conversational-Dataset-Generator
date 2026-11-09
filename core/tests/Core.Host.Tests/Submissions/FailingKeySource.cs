using Core.Platform.Keys;

namespace Core.Host.Tests.Submissions;

/// <summary>A source of the master key that cannot give it: absent, unreadable or of the wrong size, as the test asks.</summary>
internal sealed class FailingKeySource : IMasterKeySource
{
    private readonly MasterKeyFailureKind kind;

    internal FailingKeySource(MasterKeyFailureKind kind)
    {
        this.kind = kind;
    }

    public string Origin => "zqx-test";

    public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(kind switch
    {
        MasterKeyFailureKind.Absent => MasterKeyReading.Absent("zqx-test"),
        MasterKeyFailureKind.Unreadable => MasterKeyReading.Unreadable("zqx-test"),
        MasterKeyFailureKind.WrongSize => MasterKeyReading.FromBytes(new byte[3], DerivationLabels.Of(["texto-bruto/1/v1"]), CustodyEpochs.None, "zqx-test"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The kind is not one the source fails with."),
    });
}
