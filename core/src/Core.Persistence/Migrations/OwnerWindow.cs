using System.Data.Common;
using System.Globalization;
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
    private const string Tip = "SELECT sequence, digest FROM public.custody_event ORDER BY sequence DESC LIMIT 1";

    private readonly NpgsqlDataSource dataSource;
    private readonly int creating;
    private readonly int found;
    private readonly int arrival;
    private long tipSequence;
    private byte[]? tipDigest;

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
        if (found < creating)
        {
            await KeepTipAsync(connection, cancellationToken).ConfigureAwait(false);
            return true;
        }

        _ = await new CustodyVerifier(dataSource, TimeProvider.System).VerifyAsync(cancellationToken).ConfigureAwait(false);
        await KeepTipAsync(connection, cancellationToken).ConfigureAwait(false);
        return await WriteAsync(afterTheStep: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>After a step is applied: writes the event right after the step that creates it. False when the chain did not take the event.</summary>
    internal Task<bool> AfterAsync(MigrationStep applied, CancellationToken cancellationToken) =>
        applied.Version == creating ? WriteAsync(afterTheStep: true, cancellationToken) : Task.FromResult(true);

    private async Task KeepTipAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await ReadTipFromAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException noChain) when (noChain.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // A database older than the chain has no tip to keep.
        }
    }

    private async Task ReadTipFromAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var command = new NpgsqlCommand(Tip, connection);
        await using (command.ConfigureAwait(false))
        {
            var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            await using (reader.ConfigureAwait(false))
            {
                await ReadTipAsync(reader, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ReadTipAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var read = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        tipSequence = read ? reader.GetInt64(0) : 0;
        tipDigest = read ? reader[1] as byte[] : null;
    }

    private async Task<bool> WriteAsync(bool afterTheStep, CancellationToken cancellationToken)
    {
        var digest = tipDigest is null ? string.Empty : $";digest={Convert.ToHexStringLower(tipDigest)}";
        var reference = string.Create(CultureInfo.InvariantCulture, $"from={found};to={arrival};tip={tipSequence}{digest}{(afterTheStep ? ";late=1" : string.Empty)}");
        var result = await new CustodyChain(dataSource).AppendAsync(CustodyEvent.OwnerWindowOpened(arrival, reference), cancellationToken).ConfigureAwait(false);
        if (result.Outcome != CustodyOutcome.Appended)
        {
            return false;
        }

        Opening = afterTheStep ? OwnerWindowOpening.OpenedAfterTheStep : OwnerWindowOpening.Opened;
        return true;
    }
}
