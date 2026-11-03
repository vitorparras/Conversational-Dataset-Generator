using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>
/// The window of the owner credential in the chain of custody (#64): before the first pending step the chain is verified, its tip is kept and the system writes that the window opened.
/// A database older than the step that creates the events gets the event right after that step, with the tip read before it; an empty database has no chain, so no window.
/// </summary>
internal sealed class OwnerWindow
{
    /// <summary>The first version whose chain the verifier reads: the one that adds the column of the reference (0016).</summary>
    private const int FirstReadable = 16;

    private const string Tip = "SELECT 'tip=' || sequence || ';digest=' || encode(digest, 'hex') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";

    private readonly NpgsqlDataSource dataSource;
    private readonly int creating;
    private readonly int found;
    private readonly int arrival;
    private string tip = "tip=0";

    private OwnerWindow(NpgsqlDataSource dataSource, int creating, int found, int arrival)
    {
        this.dataSource = dataSource;
        this.creating = creating;
        this.found = found;
        this.arrival = arrival;
    }

    /// <summary>The name of the step that creates the events of the window.</summary>
    internal static string StepName => "0018-owner-window";

    /// <summary>What the run did with the window.</summary>
    internal OwnerWindowOpening Opening { get; private set; }

    /// <summary>The window of a run that leaves <paramref name="found" />; null when the build has no step that creates it or the database is empty.</summary>
    internal static OwnerWindow? Of(NpgsqlDataSource dataSource, IReadOnlyList<MigrationStep> steps, int found) =>
        found > 0 && steps.FirstOrDefault(step => step.Name == StepName) is { } creating ? new OwnerWindow(dataSource, creating.Version, found, steps[^1].Version) : null;

    /// <summary>
    /// Before the first step: verifies the chain where it can be read and refuses to go on if it does not hold, keeps the tip and opens the window now when the events exist.
    /// False when it did not go on.
    /// </summary>
    internal async Task<bool> BeforeTheStepsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        if (found >= FirstReadable && !await ChainHoldsAsync(cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await KeepTipAsync(connection, cancellationToken).ConfigureAwait(false);
        return found < creating || await WriteAsync(afterTheStep: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> ChainHoldsAsync(CancellationToken cancellationToken)
    {
        var verified = await new CustodyVerifier(dataSource, TimeProvider.System).VerifyAsync(cancellationToken).ConfigureAwait(false);
        return verified.Passed && verified.Recorded.Outcome == CustodyOutcome.Appended;
    }

    /// <summary>After a step is applied: writes the event right after the step that creates it. False when the chain did not take the event.</summary>
    internal Task<bool> AfterAsync(MigrationStep applied, CancellationToken cancellationToken) =>
        applied.Version == creating ? WriteAsync(afterTheStep: true, cancellationToken) : Task.FromResult(true);

    private async Task KeepTipAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(Tip, connection);
        await using (command.ConfigureAwait(false))
        {
            try
            {
                tip = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string ?? tip;
            }
            catch (PostgresException noChain) when (noChain.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                // A database older than the chain has no tip to keep.
            }
        }
    }

    private async Task<bool> WriteAsync(bool afterTheStep, CancellationToken cancellationToken)
    {
        var reference = $"from={found};to={arrival};{tip}{(afterTheStep ? ";late=1" : string.Empty)}";
        var result = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.OwnerWindowOpened(arrival, reference), cancellationToken).ConfigureAwait(false);
        if (result.Outcome != CustodyOutcome.Appended)
        {
            return false;
        }

        Opening = afterTheStep ? OwnerWindowOpening.OpenedAfterTheStep : OwnerWindowOpening.Opened;
        return true;
    }
}
