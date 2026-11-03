using Core.Persistence.Custody.Chain.Parameters;
using Npgsql;

namespace Core.Persistence.Custody.Chain.Append;

/// <summary>
/// What is done inside the transaction: the change and the event, in the order each caller asks for, the statement that adds the event, and the commit only when the event was added.
/// The transaction itself, and what a refusal of the server means, stay in <see cref="CustodyTransaction"/>.
/// </summary>
internal static class CustodyAppend
{
    internal static async Task<CustodyResult?> AfterAsync(CustodyScope scope, Func<CustodyScope, Task<CustodyEvent?>> change, CancellationToken cancellationToken)
    {
        var pending = await change(scope).ConfigureAwait(false);
        if (pending is null)
        {
            return null;
        }

        var result = await AppendAsync(scope, pending, cancellationToken).ConfigureAwait(false);
        return await CommitIfAppendedAsync(scope, result, cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<CustodyResult?> BeforeAsync(CustodyScope scope, CustodyEvent pending, Func<CustodyScope, Task<bool>> change, CancellationToken cancellationToken)
    {
        var result = await AppendAsync(scope, pending, cancellationToken).ConfigureAwait(false);
        if (result.Outcome != CustodyOutcome.Appended)
        {
            return result;
        }

        return await change(scope).ConfigureAwait(false)
            ? await CommitIfAppendedAsync(scope, result, cancellationToken).ConfigureAwait(false)
            : null;
    }

    internal static async Task<CustodyResult> CommitIfAppendedAsync(CustodyScope scope, CustodyResult result, CancellationToken cancellationToken)
    {
        if (result.Outcome == CustodyOutcome.Appended)
        {
            await scope.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return result;
    }

    internal static async Task<CustodyResult> AppendAsync(CustodyScope scope, CustodyEvent pending, CancellationToken cancellationToken)
    {
        var command = scope.CreateCommand(CustodyStatements.Append);
        await using (command.ConfigureAwait(false))
        {
            CustodyParameters.Bind(command, pending);
            try
            {
                return CustodyResult.Appended((long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!);
            }
            catch (PostgresException failed)
            {
                return CustodyTransaction.Refused(failed, pending);
            }
        }
    }
}
