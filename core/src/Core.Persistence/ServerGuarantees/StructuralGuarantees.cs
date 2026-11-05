using Core.Platform.Startup;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>The guarantees that are read from the catalog alone, without opening a record: the ones a conference and a rehearsal of a restore both confer.</summary>
internal static class StructuralGuarantees
{
    /// <summary>The mask of the structural guarantees that do not hold; a reading that cannot be made counts as one missing.</summary>
    internal static async Task<GuaranteeKinds> MissingAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var missing = await MissingAsync(new PrivilegeCheck(dataSource), GuaranteeKinds.Privilege, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new RlsForceCheck(dataSource), GuaranteeKinds.RowPolicy, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new ColumnRevokeCheck(dataSource), GuaranteeKinds.ColumnRevoke, cancellationToken).ConfigureAwait(false);
        missing |= await MissingAsync(new ImmutabilityCheck(dataSource), GuaranteeKinds.Immutability, cancellationToken).ConfigureAwait(false);
        return missing | await MissingAsync(new ProtectedColumnCheck(dataSource), GuaranteeKinds.ProtectedColumns, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<GuaranteeKinds> MissingAsync(IStartupCheck check, GuaranteeKinds code, CancellationToken cancellationToken) =>
        (await check.RunAsync(cancellationToken).ConfigureAwait(false)).Status == CheckStatus.Passed ? GuaranteeKinds.None : code;
}
