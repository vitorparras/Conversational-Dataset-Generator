using Core.Persistence.Content;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// Nothing of the text or of the sender is in the database in clear, whatever the occurrences and the marks keep (issue 82, decision 0030):
/// the dump of the database carries the digests and not what they digest.
/// </summary>
public sealed class ReplicationDumpTests
{
    private const string Text = "ZQX-ordem repassada em cadeia pelos grupos";
    private const string Sender = "ZQX-remetente-ana-silva";

    [DatabaseFact]
    public async Task TheDumpHoldsTheDigestsOfTheTextAndOfTheSenderAndNeitherOfThemInClearAsync()
    {
        await using var world = await IngestionWorld.CreateAsync();
        using var reading = MasterKeyReading.FromBytes(SyntheticCustody.KeyBytes(), DerivationLabels.None, "teste");
        var derivation = new KeyDerivation(reading.Key!, reading.Labels);
        var text = derivation.Digest("replicacao-texto/1", Text);
        var sender = derivation.Digest("replicacao-remetente/1", Sender);
        var store = new ReplicationStore(world.DataSource);
        var batch = await IngestionWorld.OpenAsync(world);
        var item = await IngestionWorld.AddItemAsync(world, batch);
        var said = new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero);
        StoredOccurrence[] occurrences = [new(item, 0, text, sender, Text.Length, said), new(item, 1, text, sender, Text.Length, said.AddMinutes(1))];
        StoredMark[] marks = [new(item, 1, item, 0)];

        await store.RecordAsync(new(1, batch), occurrences, marks, TestContext.Current.CancellationToken);
        _ = await IngestionWorld.AcceptAsync(world, batch);
        var dump = await world.Database.DumpAsync();

        Assert.Contains(text, dump, StringComparison.Ordinal);
        Assert.Contains(sender, dump, StringComparison.Ordinal);
        Assert.DoesNotContain(Text, dump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Sender, dump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ordem repassada", dump, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ana-silva", dump, StringComparison.OrdinalIgnoreCase);
    }
}
