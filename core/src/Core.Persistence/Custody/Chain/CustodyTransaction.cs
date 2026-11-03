using System.Data;
using System.Globalization;
using Core.Persistence.Custody.Chain.Append;
using Npgsql;

namespace Core.Persistence.Custody.Chain;

/// <summary>
/// The only place that adds an event to the chain, and always in a serializable transaction: the change the event is about is written in it too, and either both exist or neither does.
/// A writer that meets another at the same link of the chain is refused visibly, and never both are accepted.
/// </summary>
internal static class CustodyTransaction
{
    /// <summary>Runs the change in the transaction and then adds the event it asks for; null when the change asked for none, in which case nothing is kept.</summary>
    internal static Task<CustodyResult?> AppendAfterAsync(NpgsqlDataSource dataSource, Func<CustodyScope, Task<CustodyEvent?>> change, CancellationToken cancellationToken) =>
        RunAsync(dataSource, scope => CustodyAppend.AfterAsync(scope, change, cancellationToken), cancellationToken);

    /// <summary>
    /// Adds the event and then runs the change, so that the author is judged as they were when they acted and not as the change leaves them.
    /// Null when the change reports it wrote nothing, in which case the event is not kept either.
    /// </summary>
    internal static Task<CustodyResult?> AppendBeforeAsync(NpgsqlDataSource dataSource, CustodyEvent pending, Func<CustodyScope, Task<bool>> change, CancellationToken cancellationToken) =>
        RunAsync(dataSource, scope => CustodyAppend.BeforeAsync(scope, pending, change, cancellationToken), cancellationToken);

    internal static async Task<CustodyResult?> RunAsync(NpgsqlDataSource dataSource, Func<CustodyScope, Task<CustodyResult?>> body, CancellationToken cancellationToken)
    {
        try
        {
            return await InTransactionAsync(dataSource, body, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException failed) when (failed.SqlState == PostgresErrorCodes.SerializationFailure)
        {
            return CustodyRefusals.Conflict();
        }
    }

    private static async Task<CustodyResult?> InTransactionAsync(NpgsqlDataSource dataSource, Func<CustodyScope, Task<CustodyResult?>> body, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            await using (transaction.ConfigureAwait(false))
            {
                return await body(new CustodyScope(connection, transaction)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>What the server's refusal means; one that is none of these is a defect, and its message names the item, the step and the reason and nothing else.</summary>
    internal static CustodyResult Refused(PostgresException failed, CustodyEvent pending) => (failed.SqlState, failed.ConstraintName) switch
    {
        (PostgresErrorCodes.UniqueViolation, _) or (PostgresErrorCodes.SerializationFailure, _) => CustodyRefusals.Conflict(),
        (PostgresErrorCodes.ForeignKeyViolation, CustodyStatements.ActorConstraint) => CustodyRefusals.ActorUnknown(),
        (PostgresErrorCodes.CheckViolation, CustodyStatements.ActorActiveConstraint) => CustodyRefusals.ActorDeactivated(),
        (PostgresErrorCodes.ForeignKeyViolation, CustodyStatements.CorrectsConstraint) => CustodyRefusals.CorrectedNotFound(),
        (CustodyStatements.ImmutableSqlState, _) => CustodyRefusals.Immutable(),
        (CustodyStatements.ChainBrokenSqlState, _) => CustodyRefusals.ChainBroken(),
        (CustodyStatements.PassedAfterFailureSqlState, _) => CustodyRefusals.PassedAfterFailure(),
        _ => throw new InvalidOperationException(Describe(failed.SqlState, pending)),
    };

    private static string Describe(string? sqlState, CustodyEvent pending) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The custody chain refused the event (SQLSTATE {sqlState}) about {CustodyNames.Of(pending.Item.Kind)} {pending.Item.Id}, " +
            $"step {CustodyNames.Of(pending.Step)}, reason {CustodyNames.Of(pending.Reason)}.");
}
