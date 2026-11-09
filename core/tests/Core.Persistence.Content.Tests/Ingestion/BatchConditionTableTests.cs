using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The condition of origin as the table of the batches holds it, judged by the database with direct statements (issue 85, criterion 2): it is born with the batch, in a closed form, and never
/// changes after the opening, whoever asks, the owner and a session that skips the ordinary triggers included.
/// </summary>
public sealed class BatchConditionTableTests
{
    private const string OutsideTheRules = "CE006";
    private const string Change = "UPDATE content.ingestion_batch SET condition = 'permissiva' WHERE id = @id";
    private const string WithoutCondition = "INSERT INTO content.ingestion_batch (domain_id, opened_by) VALUES (1, 1)";
    private const string TheOtherTriggerThatRefusesAnyChange = "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_change_needs_event";

    [DatabaseFact]
    public async Task BatchWithNoConditionIsRefusedByTheTableWhoeverOpensIt()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var byApplication = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, WithoutCondition));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, WithoutCondition));

        Assert.Equal((PostgresErrorCodes.NotNullViolation, PostgresErrorCodes.NotNullViolation), (byApplication.SqlState, byOwner.SqlState));
    }

    [DatabaseTheory]
    [InlineData("Restrita")]
    [InlineData("1restrita")]
    [InlineData("_restrita")]
    [InlineData("com espaco")]
    [InlineData("com-traco")]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task ConditionThatIsNotANameInSnakeCaseIsRefusedByTheTable(string condition)
    {
        await using var world = await IngestionWorld.CreateAsync();

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAsync(1, "INSERT INTO content.ingestion_batch (domain_id, opened_by, condition) VALUES (1, 1, @condition)", ("condition", condition)));

        Assert.Equal(("ingestion_batch_condition", PostgresErrorCodes.CheckViolation), (refused.ConstraintName, refused.SqlState));
    }

    [DatabaseFact]
    public async Task ConditionOfSixtyThreeCharactersIsTheLongestTheTableHolds()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var batch = await OpenAsync(world, new string('a', 63));

        Assert.Equal(new string('a', 63), await world.InDomainAsync(1, "SELECT condition FROM content.ingestion_batch WHERE id = @id", ("id", batch)) as string);
    }

    [DatabaseFact]
    public async Task ApplicationCannotChangeTheConditionOfABatch()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Change, ("id", batch)));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task ConditionNeverChangesAfterTheOpeningNotEvenForTheOwnerOrAReplicaSession()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);
        await world.Database.RunAsOwnerAsync(TheOtherTriggerThatRefusesAnyChange);

        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Change, ("id", batch)));
        var asReplica = await Assert.ThrowsAsync<PostgresException>(() => AsReplicaAsync(world, Change, batch));

        Assert.Equal((OutsideTheRules, OutsideTheRules), (byOwner.SqlState, asReplica.SqlState));
        Assert.All([byOwner, asReplica], refused => Assert.Contains("condition", refused.MessageText, StringComparison.Ordinal));
        Assert.Equal("restrita", await world.InDomainAsync(1, "SELECT condition FROM content.ingestion_batch WHERE id = @id", ("id", batch)) as string);
    }

    [DatabaseFact]
    public async Task WritingTheConditionItAlreadyHasIsNotAChange()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var batch = await OpenAsync(world);
        await world.Database.RunAsOwnerAsync(TheOtherTriggerThatRefusesAnyChange);

        _ = await world.OwnerInDomainAsync(1, "UPDATE content.ingestion_batch SET condition = 'restrita' WHERE id = @id", ("id", batch));

        Assert.Equal("restrita", await world.InDomainAsync(1, "SELECT condition FROM content.ingestion_batch WHERE id = @id", ("id", batch)) as string);
    }

    [DatabaseFact]
    public async Task TheTriggerThatKeepsTheConditionFiresEvenWhereOrdinaryTriggersAreSkipped()
    {
        await using var world = await IngestionWorld.CreateAsync();

        var enabled = await world.ScalarAsync("SELECT tgenabled::text FROM pg_trigger WHERE tgname = 'batch_condition_is_never_changed'") as string;

        Assert.Equal("A", enabled);
    }

    private static Task<long> OpenAsync(ContentWorld world) => OpenAsync(world, "restrita");

    private static async Task<long> OpenAsync(ContentWorld world, string condition) =>
        (long)(await world.InDomainAsync(
            1,
            "WITH b AS (INSERT INTO content.ingestion_batch (domain_id, opened_by, condition) VALUES (1, 1, @condition) RETURNING id) "
            + "INSERT INTO public.custody_event (actor_person_id, item_kind, item_id, step, reason) SELECT 1, 'ingestion_batch', id, 'batch_opened', 'administrative_act' FROM b RETURNING item_id",
            ("condition", condition)))!;

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> AsReplicaAsync(ContentWorld world, string statement, long id)
    {
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using var replica = new NpgsqlCommand("SET session_replication_role = replica", connection);
        _ = await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(statement, connection);
        _ = command.Parameters.AddWithValue("id", id);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
