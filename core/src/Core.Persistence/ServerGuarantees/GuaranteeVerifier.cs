using Core.Persistence.Content.Reading;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Confers the guarantees the chain does not cover, with the application's credential and without repairing anything: the same readings the startup checks make, and a sample of sealed records,
/// and records the verdict as an event of the chain written by the system, with the mask of the guarantees that are missing. A reading that cannot be made counts as a guarantee missing.
/// </summary>
public sealed class GuaranteeVerifier
{
    private readonly NpgsqlDataSource dataSource;
    private readonly EncryptedSample? sample;
    private readonly CustodyChain chain;

    /// <summary>The conference reads and records through the data source built with the application's credential, and opens the sample with the key it reads.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="readKey">Reads the master key and its labels; the conference releases the reading.</param>
    /// <param name="readerOver">Builds the path that reads protected content over a reading of the key.</param>
    public GuaranteeVerifier(NpgsqlDataSource dataSource, Func<CancellationToken, Task<MasterKeyReading>> readKey, Func<MasterKeyReading, ProtectedContentReader> readerOver)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        sample = new EncryptedSample(dataSource, readKey, readerOver);
        chain = new CustodyChain(dataSource);
    }

    /// <summary>The conference without the sample of sealed records: nothing is deciphered, so no key is needed; the sample goes in the first round of the verification after the start.</summary>
    public GuaranteeVerifier(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        this.dataSource = dataSource;
        chain = new CustodyChain(dataSource);
    }

    /// <summary>Confers the guarantees one after the other and records the verdict; a verdict the chain did not accept is told in the answer, and the conference may be repeated.</summary>
    /// <param name="cancellationToken">Cancels the conference.</param>
    public async Task<GuaranteeVerification> VerifyAsync(CancellationToken cancellationToken)
    {
        var missing = await MissingAsync(new PrivilegeCheck(dataSource), GuaranteeKinds.Privilege, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new RlsForceCheck(dataSource), GuaranteeKinds.RowPolicy, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new ColumnRevokeCheck(dataSource), GuaranteeKinds.ColumnRevoke, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new ImmutabilityCheck(dataSource), GuaranteeKinds.Immutability, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new ProtectedColumnCheck(dataSource), GuaranteeKinds.ProtectedColumns, cancellationToken).ConfigureAwait(false);
        var sampled = await SampledAsync(cancellationToken).ConfigureAwait(false);
        missing |= sampled == SampleVerdict.NotOpened ? GuaranteeKinds.EncryptedSample : GuaranteeKinds.None;
        var recorded = await chain.AppendAsync(CustodyEvent.GuaranteesVerified((long)missing), cancellationToken).ConfigureAwait(false);
        return new GuaranteeVerification(missing, recorded, sampled == SampleVerdict.NoneOutsideLoss, sampled == SampleVerdict.SearchStopped);
    }

    private async Task<SampleVerdict> SampledAsync(CancellationToken cancellationToken) =>
        sample is null ? SampleVerdict.Opened : await sample.VerdictAsync(cancellationToken).ConfigureAwait(false);

    private static async Task<GuaranteeKinds> MissingAsync(IStartupCheck check, GuaranteeKinds code, CancellationToken cancellationToken) =>
        (await check.RunAsync(cancellationToken).ConfigureAwait(false)).Status == CheckStatus.Passed ? GuaranteeKinds.None : code;
}
