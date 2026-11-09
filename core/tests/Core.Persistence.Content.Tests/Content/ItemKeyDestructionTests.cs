using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// What emptying an item destroys (decision 0049): the key of the item goes with its content, and a copy of the bytes taken before cannot be opened once the epoch that wrapped the
/// key is destroyed, not even with the master key, every label and the newest epoch.
/// </summary>
[Trait("Category", "Database")]
public sealed class ItemKeyDestructionTests
{
    private const string Raw = "texto bruto sintetico zqx";
    private const string Protocol = "PROT-2026/0042";
    private const string ItemKeyColumns = "SELECT coalesce(dek_epoch::text, 'nulo') || ',' || coalesce(length(dek_wrapped)::text, 'nulo') FROM content.item_text WHERE id = @id";
    private const string MapKeyColumns = "SELECT coalesce(dek_epoch::text, 'nulo') || ',' || coalesce(length(dek_wrapped)::text, 'nulo') FROM content.reversal_map WHERE id = @id";

    [DatabaseFact]
    public async Task EachRowHasAKeyOfItsOwnAndEmptyingTheItemDestroysTheKeysOfTheItemAndOfItsMaps()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, "sanitizado"), TestContext.Current.CancellationToken);
        var other = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, "sanitizado"), TestContext.Current.CancellationToken);
        var map = await world.Writer.AddReversalMapAsync(1, item, "mapa zqx", TestContext.Current.CancellationToken);
        var before = (await WrappedAsync(world, "item_text", item), await WrappedAsync(world, "item_text", other), await WrappedAsync(world, "reversal_map", map));

        _ = await EmptyAsync(world, item);

        var live = await InDomainAsync(world, ItemKeyColumns, other);
        Assert.Equal((false, false, false), (before.Item1.SequenceEqual(before.Item2), before.Item1.SequenceEqual(before.Item3), before.Item2.SequenceEqual(before.Item3)));
        var emptied = (await InDomainAsync(world, ItemKeyColumns, item), await InDomainAsync(world, MapKeyColumns, map));
        Assert.Equal(("nulo,nulo", "nulo,nulo", true), (emptied.Item1, emptied.Item2, live.StartsWith("1,", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task CopyOfTheBytesTakenBeforeEmptyingCannotBeOpenedOnceTheEpochIsRotatedAndDestroyed()
    {
        await using var world = await ContentWorld.CreateAsync();
        var item = await world.Writer.AddItemTextAsync(1, ItemTexts.Of(Raw, "sanitizado"), TestContext.Current.CancellationToken);
        var kept = await world.Writer.AddItemTextAsync(1, ItemTexts.Of("outro texto zqx"), TestContext.Current.CancellationToken);
        var copy = new ContentKey(1, await WrappedAsync(world, "item_text", item));
        var copiedContent = await world.StoredAsync(1, ContentQueries.ReadRawText, item);
        using var epoch1 = SyntheticCustody.Holding(ContentWorld.Labels);
        Assert.Equal(Raw, epoch1.Content.Open(ContentColumns.RawText, 1, copiedContent, copy));

        _ = await EmptyAsync(world, item);
        using var epochs12 = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);
        var rewrapped = await new EpochRotation(world.DataSource, epochs12.Keys).RotateAsync(TestContext.Current.CancellationToken);
        using var epoch2 = SyntheticCustody.HoldingEpochs([2], ContentWorld.Labels);

        var withoutOldEpoch = Assert.Throws<EnvelopeRefusedException>(() => epoch2.Content.Open(ContentColumns.RawText, 1, copiedContent, copy));
        var secretOfTwoUnderTheNumberOne = new Dictionary<int, byte[]> { [1] = SyntheticCustody.SecretOf(2), [2] = SyntheticCustody.SecretOf(2) };
        using var guessed = SyntheticCustody.HoldingSecrets(secretOfTwoUnderTheNumberOne, ContentWorld.Labels);
        var withTheNewSecret = Assert.Throws<EnvelopeRefusedException>(() => guessed.Content.Open(ContentColumns.RawText, 1, copiedContent, copy));
        var stillRead = await world.ReaderWith(epoch2).ReadAsync(new ContentRead(CustodyAuthor.System, ContentColumns.RawText, 1, kept), TestContext.Current.CancellationToken);

        Assert.Equal((1, "outro texto zqx"), (rewrapped, stillRead.Text));
        Assert.Contains("not among the loaded ones", withoutOldEpoch.Message, StringComparison.Ordinal);
        Assert.NotEmpty(withTheNewSecret.Message);
    }

    private static async Task<byte[]> WrappedAsync(ContentWorld world, string table, long id) =>
        await world.StoredAsync(1, $"SELECT dek_wrapped FROM content.{table} WHERE id = @id", id);

    private static async Task<string> InDomainAsync(ContentWorld world, string statement, long id) => (string)(await world.InDomainAsync(1, statement, ("id", id)))!;

    private static async Task<EmptyingOutcome> EmptyAsync(ContentWorld world, long item)
    {
        var curator = await world.PersonAsync("zqx-curator");
        var emptied = await world.Emptier.EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(curator), 1, item, EmptyingMotive.ContractRequirement, Protocol), TestContext.Current.CancellationToken);
        return emptied.Outcome;
    }
}
