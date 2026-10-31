using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// An item is emptied by marking it and clearing every content column, in clear and sealed alike; the columns allow it and refuse the halves,
/// and the marker is in clear, so that an emptied item is found without reading a sealed column. The emptying itself is another step's.
/// </summary>
[Trait("Category", "Database")]
public sealed class ProtectedContentEmptyingTests
{
    private const string Empty = "UPDATE content.item_text SET emptied_at = now(), raw_text = NULL, sanitized_text = NULL, pseudonymized_text = NULL WHERE id = @id RETURNING id";

    [DatabaseFact]
    public async Task ItemIsEmptiedByMarkingItAndClearingEveryContentColumn()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto", "sanitizado", "pseudonimizado"), TestContext.Current.CancellationToken);

        var emptied = await world.OwnerInDomainAsync(1, Empty, ("id", item));

        Assert.Equal(item, emptied);
    }

    [DatabaseFact]
    public async Task EmptiedItemIsFoundByItsMarkerWithoutReadingASealedColumn()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);
        _ = await world.OwnerInDomainAsync(1, Empty, ("id", item));

        var found = await world.InDomainAsync(1, "SELECT id FROM content.item_text WHERE emptied_at IS NOT NULL");

        Assert.Equal(item, found);
    }

    [DatabaseFact]
    public async Task MarkingAnItemEmptiedWhileItStillHoldsTheTextIsRefused()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "UPDATE content.item_text SET emptied_at = now() WHERE id = @id", ("id", item)));

        Assert.Equal("item_text_emptied", refused.ConstraintName);
    }

    [DatabaseFact]
    public async Task ClearingTheSealedTextWithoutMarkingTheItemIsRefused()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto", "sanitizado"), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "UPDATE content.item_text SET raw_text = NULL WHERE id = @id", ("id", item)));

        Assert.Equal("item_text_emptied", refused.ConstraintName);
    }

    [DatabaseFact]
    public async Task EmptyingTheMapNeedsTheMarkerAndTheClearedEntriesTogether()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(1, item, "entradas", TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, "UPDATE content.reversal_map SET entries = NULL WHERE id = @id", ("id", map)));
        var emptied = await world.OwnerInDomainAsync(1, "UPDATE content.reversal_map SET emptied_at = now(), entries = NULL WHERE id = @id RETURNING id", ("id", map));

        Assert.Equal(("reversal_map_emptied", map), (refused.ConstraintName, emptied));
    }

    [DatabaseFact]
    public async Task ApplicationRoleCannotChangeContentOnceItIsWritten()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, new NewItemText("bruto"), TestContext.Current.CancellationToken);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.InDomainAsync(1, Empty, ("id", item)));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, refused.SqlState);
    }
}
