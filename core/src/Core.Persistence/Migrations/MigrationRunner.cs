using System.Diagnostics;
using Npgsql;

namespace Core.Persistence.Migrations;

/// <summary>Brings the schema to the last step this build carries, one transaction per step, under a server-wide lock.</summary>
public sealed class MigrationRunner
{
    private const string Lock = "SELECT pg_advisory_lock(hashtextextended('conversational-dataset-generator.migrations', 0))";
    private const string Unlock = "SELECT pg_advisory_unlock(hashtextextended('conversational-dataset-generator.migrations', 0))";
    private const string Register = "INSERT INTO " + SchemaRegistry.Table + " (version, name, sha256) VALUES (@version, @name, @sha256)";

    private readonly NpgsqlDataSource dataSource;
    private readonly IReadOnlyList<MigrationStep> steps;

    /// <summary>The runner over the data source built with the owner credential, and the steps in order of version.</summary>
    public MigrationRunner(NpgsqlDataSource dataSource, IReadOnlyList<MigrationStep> steps)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(steps);
        this.dataSource = dataSource;
        this.steps = steps;
    }

    /// <summary>Applies what is pending, or refuses without running anything when the registry is ahead of this build or diverges from it.</summary>
    public async Task<MigrationResult> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using (connection.ConfigureAwait(false))
            {
                return await RunLockedAsync(connection, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (NpgsqlException exception)
        {
            return new MigrationResult { Outcome = MigrationOutcome.NotConnected, SqlState = (exception as PostgresException)?.SqlState };
        }
    }

    private async Task<MigrationResult> RunLockedAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, transaction: null, new NpgsqlBatchCommand(Lock), cancellationToken).ConfigureAwait(false);
        try
        {
            var comparison = SchemaComparison.Compare(steps, await SchemaRegistry.ReadAsync(connection, cancellationToken).ConfigureAwait(false));
            return comparison.State switch
            {
                SchemaState.UpToDate => new MigrationResult { Outcome = MigrationOutcome.NothingToDo, Found = comparison.Found, Expected = comparison.Expected },
                SchemaState.Behind => await ApplyAsync(connection, comparison, cancellationToken).ConfigureAwait(false),
                SchemaState.Ahead or SchemaState.Divergent => new MigrationResult
                {
                    Outcome = comparison.State == SchemaState.Ahead ? MigrationOutcome.RefusedAhead : MigrationOutcome.RefusedDivergent,
                    Found = comparison.Found,
                    Expected = comparison.Expected,
                    Step = comparison.Step,
                    Registered = comparison.Registered,
                    Current = comparison.Current,
                },
                _ => throw new UnreachableException(),
            };
        }
        finally
        {
            await ReleaseAsync(connection).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, NpgsqlBatchCommand command, CancellationToken cancellationToken)
    {
        var batch = new NpgsqlBatch(connection, transaction) { BatchCommands = { command } };
        await using (batch.ConfigureAwait(false))
        {
            _ = await batch.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ReleaseAsync(NpgsqlConnection connection)
    {
        try
        {
            await ExecuteAsync(connection, transaction: null, new NpgsqlBatchCommand(Unlock), CancellationToken.None).ConfigureAwait(false);
        }
        catch (NpgsqlException)
        {
            // The lock went with the lost session.
        }
    }

    private static async Task ApplyStepAsync(NpgsqlConnection connection, MigrationStep step, CancellationToken cancellationToken)
    {
        var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using (transaction.ConfigureAwait(false))
        {
            await StepCommand.RunAsync(connection, transaction, step, cancellationToken).ConfigureAwait(false);
            var register = new NpgsqlBatchCommand(Register);
            _ = register.Parameters.AddWithValue("version", step.Version);
            _ = register.Parameters.AddWithValue("name", step.Name);
            _ = register.Parameters.AddWithValue("sha256", step.Sha256);
            await ExecuteAsync(connection, transaction, register, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<MigrationResult> ApplyAsync(NpgsqlConnection connection, SchemaComparison comparison, CancellationToken cancellationToken)
    {
        var applied = new List<string>();
        var found = comparison.Found;
        foreach (var step in steps.Skip(comparison.Applied))
        {
            try
            {
                await ApplyStepAsync(connection, step, cancellationToken).ConfigureAwait(false);
            }
            catch (NpgsqlException exception)
            {
                return new MigrationResult
                {
                    Outcome = MigrationOutcome.StepFailed,
                    Found = found,
                    Expected = comparison.Expected,
                    Step = step.Name,
                    SqlState = (exception as PostgresException)?.SqlState,
                    Applied = applied,
                };
            }

            applied.Add(step.Name);
            found = step.Version;
        }

        return new MigrationResult { Outcome = MigrationOutcome.Applied, Found = found, Expected = comparison.Expected, Applied = applied };
    }
}
