using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// What the server keeps of an emptied item whoever writes (#73, migration 0016): the row is final, nothing is added to it, the application role may clear the content columns and nothing else,
/// and the refusals carry their own SQLSTATE.
/// </summary>
[Trait("Category", "Database")]
public sealed class ContentEmptiedTriggerTests
{
    private const string Emptied = "CE004";
    private const string EmptyItem = "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL, "
        + "dek_epoch = NULL, dek_wrapped = NULL WHERE id = @id RETURNING id";
    private const string InsertMap = "INSERT INTO content.reversal_map (domain_id, item_id, entries, dek_epoch, dek_wrapped) VALUES (1, @id, '\\x01', 1, '\\x01') RETURNING id";
    private const string EmptyMap = "UPDATE content.reversal_map SET emptied_at = now(), entries = NULL, dek_epoch = NULL, dek_wrapped = NULL WHERE item_id = @id RETURNING id";

    [DatabaseFact]
    public async Task MapIsNotAddedToAnItemThatWasEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await EmptiedItemAsync(world);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, InsertMap, ("id", item)));

        var maps = (long)(await world.OwnerInDomainAsync(1, "SELECT count(*) FROM content.reversal_map WHERE item_id = @id", ("id", item)))!;
        Assert.Equal((Emptied, 1L), (refused.SqlState, maps));
    }

    [DatabaseTheory]
    [InlineData("UPDATE content.item_text SET sanitized_text = '\\x00' WHERE id = @id")]
    [InlineData("UPDATE content.item_text SET pseudonymized_text = 'texto' WHERE id = @id")]
    [InlineData("UPDATE content.item_text SET emptied_at = NULL WHERE id = @id")]
    [InlineData("UPDATE content.reversal_map SET emptied_at = NULL WHERE item_id = @id")]
    [InlineData("UPDATE content.reversal_map SET entries = '\\x00' WHERE item_id = @id")]
    public async Task AnEmptiedRowIsFinalToTheApplicationRoleAndToTheOwnerAlike(string change)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await EmptiedItemAsync(world);

        var byApplication = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, change, ("id", item)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, change, ("id", item)));

        Assert.Equal((Emptied, Emptied), (byApplication.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task ApplicationRoleClearsTheContentColumnsAndTheMarkerOfAnItemThatWasNotEmptied()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto", "sanitizado", "pseudonimizado"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa", TestContext.Current.CancellationToken);

        var emptiedItem = await world.InDomainAsync(1, EmptyItem, ("id", item));
        var emptiedMap = await world.InDomainAsync(1, EmptyMap, ("id", item));

        Assert.NotNull(emptiedItem);
        Assert.NotNull(emptiedMap);
    }

    [DatabaseTheory]
    [InlineData("UPDATE content.item_text SET raw_text = '\\x01' WHERE id = @id")]
    [InlineData("UPDATE content.item_text SET sanitized_text = '\\x01' WHERE id = @id")]
    [InlineData("UPDATE content.item_text SET pseudonymized_text = 'outro texto' WHERE id = @id")]
    [InlineData("UPDATE content.item_text SET raw_text = NULL WHERE id = @id")]
    [InlineData("UPDATE content.reversal_map SET entries = '\\x01' WHERE item_id = @id")]
    [InlineData("UPDATE content.reversal_map SET entries = NULL WHERE item_id = @id")]
    [InlineData("UPDATE content.reversal_map SET emptied_at = now(), entries = NULL WHERE item_id = @id")]
    public async Task ContentOfARowThatWasNotEmptiedIsNotChangedByTheApplicationRoleNorByTheOwner(string change)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto", "sanitizado", "pseudonimizado"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa", TestContext.Current.CancellationToken);

        var byApplication = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, change, ("id", item)));
        var byOwner = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, change, ("id", item)));

        Assert.Equal((Emptied, Emptied), (byApplication.SqlState, byOwner.SqlState));
    }

    [DatabaseFact]
    public async Task ApplicationRoleStillChangesNothingOfTheMapButItsMarkerAndEntries()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa", TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, "UPDATE content.reversal_map SET recorded_at = now() WHERE item_id = @id", ("id", item)));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    [DatabaseFact]
    public async Task EmptyingRecordsTheItemOnceAndAnItemNotEmptiedIsNotRecorded()
    {
        await using var world = await ContentWorld.CreateAsync();
        var emptied = await EmptiedItemAsync(world);
        var kept = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);

        var recorded = await world.InDomainAsync(1, "SELECT array_agg(item_id ORDER BY item_id) FROM content.item_emptied");

        Assert.Equal(new[] { emptied }, (long[])recorded!);
        Assert.DoesNotContain(kept, (long[])recorded!);
    }

    [DatabaseTheory]
    [InlineData("INSERT INTO content.item_emptied (domain_id, item_id) VALUES (1, @id)")]
    [InlineData("UPDATE content.item_emptied SET item_id = item_id + 1 WHERE item_id = @id")]
    [InlineData("DELETE FROM content.item_emptied WHERE item_id = @id")]
    public async Task ApplicationRoleReadsTheItemsEmptiedAndNeverWritesThem(string change)
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await EmptiedItemAsync(world);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, change, ("id", item)));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }

    private static async Task<long> EmptiedItemAsync(ContentWorld world)
    {
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto", "sanitizado", "pseudonimizado"), TestContext.Current.CancellationToken);
        _ = await world.Writer.AddReversalMapAsync(1, item, "mapa de reversao zqx", TestContext.Current.CancellationToken);
        _ = await world.InDomainAsync(1, EmptyItem, ("id", item));
        _ = await world.InDomainAsync(1, EmptyMap, ("id", item));
        return item;
    }
}
