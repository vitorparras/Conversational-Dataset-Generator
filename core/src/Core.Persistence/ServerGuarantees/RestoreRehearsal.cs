using System.Diagnostics;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// The rehearsal of a restore, over the database the copy was put back into, with the credential of the application: the links of the chain, the chain against the seal noted outside the copy,
/// and the guarantees of the server that the catalog tells, and the result recorded as an event of the chain, written by the system, that says it held or what did not. Nothing here opens a record
/// of the content: the sample of sealed records that the conference of the guarantees takes is left out, so no event of reading is written.
/// </summary>
public sealed class RestoreRehearsal
{
    private readonly NpgsqlDataSource dataSource;
    private readonly CustodyVerifier verifier;
    private readonly CustodyChain chain;

    /// <summary>The rehearsal reads and records through the data source built with the application's credential.</summary>
    /// <param name="dataSource">The source of the connections.</param>
    /// <param name="time">Tells the moment of the verification of the chain.</param>
    public RestoreRehearsal(NpgsqlDataSource dataSource, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(time);
        this.dataSource = dataSource;
        verifier = new CustodyVerifier(dataSource, time);
        chain = new CustodyChain(dataSource);
    }

    /// <summary>The names of what did not hold, apart by a dot, in the order of the bits; <c>held</c> for none.</summary>
    public static string NamesOf(RehearsalFindings findings)
    {
        var names = Enum.GetValues<RehearsalFindings>()
            .Where(finding => finding != RehearsalFindings.None && findings.HasFlag(finding))
            .Select(NameOf)
            .ToList();
        return names.Count == 0 ? NameOf(RehearsalFindings.None) : string.Join('.', names);
    }

    private static string NameOf(RehearsalFindings finding) => finding switch
    {
        RehearsalFindings.None => "held",
        RehearsalFindings.Privilege => "privilege",
        RehearsalFindings.Policy => "policy",
        RehearsalFindings.Revoke => "revoke",
        RehearsalFindings.Immutable => "immutable",
        RehearsalFindings.Columns => "columns",
        RehearsalFindings.Chain => "chain",
        RehearsalFindings.Seal => "seal",
        _ => throw new UnreachableException(),
    };

    /// <summary>Rehearses and records the result; the result is recorded only if the links hold, because the chain takes no event but a verification while they do not.</summary>
    /// <param name="seal">The seal noted outside the copy, which the chain must have.</param>
    /// <param name="cancellationToken">Cancels the rehearsal.</param>
    public Task<RehearsalResult> RehearseAsync(ChainSeal seal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(seal);
        return ConcludeAsync(seal, cancellationToken);
    }

    private async Task<RehearsalResult> ConcludeAsync(ChainSeal seal, CancellationToken cancellationToken)
    {
        var verification = await verifier.VerifyAsync(seal, cancellationToken).ConfigureAwait(false);
        var findings = (RehearsalFindings)(long)await StructuralGuarantees.MissingAsync(dataSource, cancellationToken).ConfigureAwait(false);
        findings |= verification.Passed ? RehearsalFindings.None : RehearsalFindings.Chain;
        findings |= verification.Seal is { Holds: true } ? RehearsalFindings.None : RehearsalFindings.Seal;
        var recorded = await chain.AppendAsync(CustodyEvent.RestoreRehearsed((long)findings, NamesOf(findings)), cancellationToken).ConfigureAwait(false);
        return new RehearsalResult(findings, verification, recorded);
    }
}
