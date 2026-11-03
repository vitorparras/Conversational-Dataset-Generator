using System.Diagnostics.CodeAnalysis;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The database ties the emptying of an item and the change of its wrapped key to an event of the chain written in the same transaction (migration 0018): without it the change is
/// refused for every role, the owner included, and the content stays as it was. An event of another transaction, of another item or of another epoch does not release it.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentChangeNeedsEventTests
{
    private const string Unbound = "CE005";
    private const string Empty = "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL, "
        + "dek_epoch = NULL, dek_wrapped = NULL WHERE id = @id RETURNING id";
    private const string EmptyMap = "UPDATE content.reversal_map SET emptied_at = now(), entries = NULL, dek_epoch = NULL, dek_wrapped = NULL WHERE item_id = @id RETURNING id";
    private const string RewrapItem = "UPDATE content.item_text SET dek_epoch = 2, dek_wrapped = '\\x02' WHERE id = @id RETURNING id";
    private const string RewrapMap = "UPDATE content.reversal_map SET dek_epoch = 2, dek_wrapped = '\\x02' WHERE item_id = @id RETURNING id";
    private const string Intact = "SELECT count(*) FROM content.item_text WHERE id = @id AND emptied_at IS NULL AND raw_text IS NOT NULL AND dek_epoch = 1";

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItemEmptiedWithoutTheEventOfItsEmptyingIsRefusedAndStaysWhole(bool byOwner)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => InDomainAsync(world, byOwner, Empty, item));

        Assert.Equal((Unbound, 1L), (refused.SqlState, await world.OwnerInDomainAsync(1, Intact, ("id", item))));
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MapEmptiedWithoutTheEventOfItsItemIsRefused(bool byOwner)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);
        _ = await world.OwnerInDomainAfterAsync(ContentWorld.EmptiedEvent, 1, Empty, ("id", item));

        var refused = await Assert.ThrowsAsync<PostgresException>(() => InDomainAsync(world, byOwner, EmptyMap, item));

        Assert.Equal(Unbound, refused.SqlState);
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ItemEmptiedWithTheEventOfItsEmptyingInTheSameTransactionIsEmptied(bool byOwner)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);

        var emptied = await (byOwner ? world.OwnerInDomainAfterAsync(ContentWorld.EmptiedEvent, 1, Empty, ("id", item)) : world.InDomainAfterAsync(ContentWorld.EmptiedEvent, 1, Empty, ("id", item)));

        Assert.Equal(item, emptied);
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrappedKeyChangedWithoutTheEventOfARotationIsRefusedForItemAndMap(bool byOwner)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);

        var onItem = await Assert.ThrowsAsync<PostgresException>(() => InDomainAsync(world, byOwner, RewrapItem, item));
        var onMap = await Assert.ThrowsAsync<PostgresException>(() => InDomainAsync(world, byOwner, RewrapMap, item));

        Assert.Equal((Unbound, Unbound, 1L), (onItem.SqlState, onMap.SqlState, await world.OwnerInDomainAsync(1, Intact, ("id", item))));
    }

    [DatabaseFact]
    public async Task WrappedKeyChangedWithTheEventOfTheRotationToThatEpochIsChanged()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);

        var onItem = await world.InDomainAfterAsync(ContentWorld.RotatedEvent, 1, RewrapItem, ("id", item), ("epoch", 2));
        var onMap = await world.InDomainAfterAsync(ContentWorld.RotatedEvent, 1, RewrapMap, ("id", item), ("epoch", 2));

        Assert.Equal((item, true), (onItem, onMap is not null));
    }

    [DatabaseFact]
    public async Task EventOfARotationToAnotherEpochDoesNotReleaseTheChange()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAfterAsync(ContentWorld.RotatedEvent, 1, RewrapItem, ("id", item), ("epoch", 3)));

        Assert.Equal(Unbound, refused.SqlState);
    }

    [DatabaseFact]
    public async Task EventOfAnotherItemDoesNotReleaseTheEmptying()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);
        var other = await ItemWithMapAsync(world);

        var refused = await Assert.ThrowsAsync<PostgresException>(
            () => world.OwnerInDomainAfterAsync(ContentWorld.EmptiedEventOfItem, 1, Empty, ("id", item), ("item", other)));

        Assert.Equal((Unbound, 1L), (refused.SqlState, await world.OwnerInDomainAsync(1, Intact, ("id", item))));
    }

    [DatabaseFact]
    public async Task EventOfAnEarlierTransactionDoesNotReleaseTheEmptying()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);
        _ = await world.ScalarAsync(ContentWorld.EmptiedEvent, ("id", item));

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Empty, ("id", item)));

        Assert.Equal((Unbound, 1L), (refused.SqlState, await world.OwnerInDomainAsync(1, Intact, ("id", item))));
    }

    [DatabaseFact]
    public async Task EventOfAConcurrentTransactionNotYetCommittedDoesNotReleaseTheEmptying()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);
        await using var connection = await Sql.OpenAsync(world.Database.OwnerConnectionString);
        await using var open = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var writing = new NpgsqlCommand(ContentWorld.EmptiedEvent, connection, open);
        _ = writing.Parameters.AddWithValue("id", item);
        _ = await writing.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, Empty, ("id", item)));

        Assert.Equal((Unbound, 1L), (refused.SqlState, await world.OwnerInDomainAsync(1, Intact, ("id", item))));
    }

    [DatabaseFact]
    public async Task EventWrittenInsideASavepointFailsClosed()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);
        var inSavepoint = "SAVEPOINT zqx; " + ContentWorld.EmptiedEvent + "; RELEASE SAVEPOINT zqx";

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAfterAsync(inSavepoint, 1, Empty, ("id", item)));

        Assert.Equal(Unbound, refused.SqlState);
    }

    [DatabaseFact]
    public async Task EventOfARotationIsTheSystemsAboutAnEpochAndNothingElse()
    {
        await using var world = await ContentWorld.CreateAsync();
        const string Columns = "INSERT INTO public.custody_event (actor_person_id, actor_system, item_kind, item_id, step, reason) VALUES ";
        const string ByAPerson = Columns + "(3, false, 'content_epoch', 2, 'epoch_rotated', 'administrative_act')";
        const string AboutAnItem = Columns + "(NULL, true, 'item_text', 2, 'epoch_rotated', 'administrative_act')";

        var byAPerson = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(ByAPerson));
        var aboutAnItem = await Assert.ThrowsAsync<PostgresException>(() => world.ScalarAsync(AboutAnItem));

        Assert.Equal(("custody_event_rotation_shape", "custody_event_rotation_shape"), (byAPerson.ConstraintName, aboutAnItem.ConstraintName));
    }

    [DatabaseFact]
    public async Task TheRefusalHoldsInASessionThatTurnedTheTriggersOffAsAReplica()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await ItemWithMapAsync(world);
        await using var connection = await Sql.OpenAsync(world.Database.SuperuserConnectionString);
        await using (var replica = new NpgsqlCommand("SET session_replication_role = replica", connection))
        {
            _ = await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var show = new NpgsqlCommand("SHOW session_replication_role", connection);
        var mode = (string?)await show.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        var onItem = await Assert.ThrowsAsync<PostgresException>(() => ChangeAsync(connection, Empty, item));
        var onMap = await Assert.ThrowsAsync<PostgresException>(() => ChangeAsync(connection, RewrapMap, item));

        Assert.Equal(("replica", Unbound, Unbound, 1L), (mode, onItem.SqlState, onMap.SqlState, await world.OwnerInDomainAsync(1, Intact, ("id", item))));
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the tests, never input.")]
    private static async Task<object?> ChangeAsync(NpgsqlConnection connection, string statement, long item)
    {
        await using var command = new NpgsqlCommand(statement, connection);
        _ = command.Parameters.AddWithValue("id", item);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private static Task<object?> InDomainAsync(ContentWorld world, bool byOwner, string statement, long item) =>
        byOwner ? world.OwnerInDomainAsync(1, statement, ("id", item)) : world.InDomainAsync(1, statement, ("id", item));

    private static async Task<long> ItemWithMapAsync(ContentWorld world)
    {
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("texto bruto zqx", "texto sanitizado zqx", "texto pseudonimizado zqx"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa de reversao zqx", TestContext.Current.CancellationToken);
        return item;
    }
}
