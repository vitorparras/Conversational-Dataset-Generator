using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>
/// The window of the owner credential in the chain of custody (#64): before the first step that is pending the chain is verified, its tip is kept and the system writes that the window opened,
/// with what it is for and the tip it found. A database older than the step that creates the events of the window gets the event right after that step, with the tip read before it.
/// An empty database has no chain and no data to guard, so it has no window.
/// </summary>
internal sealed class OwnerWindow
{
    private const string TableExists = "SELECT pg_catalog.to_regclass('public.custody_event') IS NOT NULL";
    private const string Tip = "SELECT sequence, digest FROM public.custody_event ORDER BY sequence DESC LIMIT 1";

    private readonly NpgsqlDataSource dataSource;
    private readonly int creating;
    private readonly int found;
    private readonly int arrival;
    private ChainTip tip = ChainTip.None;

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

    /// <summary>Before the first step: opens the window now when the events exist, otherwise only keeps the tip. False when the chain did not take the event.</summary>
    internal async Task<bool> BeforeTheStepsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        if (found >= creating)
        {
            _ = await new CustodyVerifier(dataSource, TimeProvider.System).VerifyAsync(cancellationToken).ConfigureAwait(false);
            tip = await TipAsync(connection, cancellationToken).ConfigureAwait(false);
            return await WriteAsync(afterTheStep: false, cancellationToken).ConfigureAwait(false);
        }

        tip = await TipAsync(connection, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>After a step is applied: writes the event right after the step that creates it. False when the chain did not take the event.</summary>
    internal Task<bool> AfterAsync(MigrationStep applied, CancellationToken cancellationToken) =>
        applied.Version == creating ? WriteAsync(afterTheStep: true, cancellationToken) : Task.FromResult(true);

    private static async Task<ChainTip> TipAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var exists = new NpgsqlCommand(TableExists, connection);
        await using (exists.ConfigureAwait(false))
        {
            if (!(bool)(await exists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!)
            {
                return ChainTip.None;
            }
        }

        var command = new NpgsqlCommand(Tip, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? new ChainTip(reader.GetInt64(0), (byte[])reader[1]) : ChainTip.None;
            }
        }
    }

    private async Task<bool> WriteAsync(bool afterTheStep, CancellationToken cancellationToken)
    {
        var reference = OwnerWindowReference.Opened(found, arrival, tip, afterTheStep);
        var result = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.OwnerWindowOpened(arrival, reference), cancellationToken).ConfigureAwait(false);
        if (result.Outcome != CustodyOutcome.Appended)
        {
            return false;
        }

        Opening = afterTheStep ? OwnerWindowOpening.OpenedAfterTheStep : OwnerWindowOpening.Opened;
        return true;
    }
}
