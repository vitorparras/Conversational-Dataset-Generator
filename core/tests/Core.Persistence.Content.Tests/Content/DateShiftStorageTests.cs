using System.Text;
using Core.Ingestion.Contracts.Dating;
using Core.Persistence.Content;
using Core.Persistence.Content.Dating;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Npgsql;
using NpgsqlTypes;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The shift of the dates of a domain as the database keeps it (issue 108): drawn once and never changed, the second of two draws at once reading the first, each domain on its own
/// with nothing of another, sealed in a copy, carried by the rotations of the key, and a row that does not open reported lost without a new draw.
/// </summary>
[Trait("Category", "Database")]
public sealed class DateShiftStorageTests
{
    private const string Rows = "SELECT count(*) FROM content.date_shift";
    private const string Wrapped = "SELECT dek_wrapped FROM content.date_shift";
    private const string WaitingToInsert = "SELECT count(*) FROM pg_stat_activity WHERE datname = @database AND wait_event_type = 'Lock' AND query LIKE 'INSERT INTO content.date_shift%'";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task DateShiftIsDrawnOnceAndNeverChanges()
    {
        await using var world = await ContentWorld.CreateAsync();
        var first = await world.DateShifts.EstablishAsync(1, Token);

        var again = await world.DateShifts.EstablishAsync(1, Token);

        Assert.Equal((DateShiftOutcome.Kept, first.Shift!.Days, 1L), (again.Outcome, again.Shift?.Days, (long)(await world.OwnerInDomainAsync(1, Rows))!));
    }

    [DatabaseTheory]
    [InlineData("UPDATE content.date_shift SET shift = '\\x00'::bytea", "CE006")]
    [InlineData("UPDATE content.date_shift SET drawn_at = now() - interval '1 day'", "CE006")]
    [InlineData("UPDATE content.date_shift SET dek_epoch = dek_epoch", "CE005")]
    [InlineData("DELETE FROM content.date_shift", "CE006")]
    public async Task RowOfTheShiftRefusesEveryChangeButTheRewrapAndEveryRemoval(string statement, string refusal)
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.DateShifts.EstablishAsync(1, Token);

        var refused = await Assert.ThrowsAsync<PostgresException>(() => world.OwnerInDomainAsync(1, statement));

        Assert.Equal(refusal, refused.SqlState);
    }

    [DatabaseFact]
    public async Task TwoDrawsAtOnceKeepTheFirstShift()
    {
        await using var world = await ContentWorld.CreateAsync();
        var drawing = await ContentTransaction.BeginAsync(world.DataSource, 1, Token);
        await using (drawing.ConfigureAwait(false))
        {
            await KeepUncommittedAsync(world, drawing, "-1234");
            var second = world.DateShifts.EstablishAsync(1, Token);
            await WaitForTheSecondToBlockAsync(world);

            await drawing.CommitAsync(Token);

            var kept = await second.WaitAsync(TimeSpan.FromSeconds(30), Token);
            Assert.Equal((DateShiftOutcome.Kept, -1234, 1L), (kept.Outcome, kept.Shift?.Days, (long)(await world.OwnerInDomainAsync(1, Rows))!));
        }
    }

    [DatabaseFact]
    public async Task TwoDomainsHaveDifferentShiftsWithNothingInCommon()
    {
        await using var world = await ContentWorld.CreateAsync();
        var first = await world.DateShifts.EstablishAsync(1, Token);

        _ = await world.DateShifts.EstablishAsync(2, Token);

        var (shiftOfOne, keyOfOne) = await StoredAsync(world, 1);
        var (shiftOfTwo, keyOfTwo) = await StoredAsync(world, 2);
        Assert.Equal(
            ("deslocamento-datas/1/v1", "deslocamento-datas/1/v1", "deslocamento-datas/2/v1", "deslocamento-datas/2/v1"),
            (EnvelopeCipher.LabelOf(shiftOfOne), EnvelopeCipher.LabelOf(keyOfOne.Wrapped.Span), EnvelopeCipher.LabelOf(shiftOfTwo), EnvelopeCipher.LabelOf(keyOfTwo.Wrapped.Span)));
        Assert.Equal((false, false), (shiftOfOne.SequenceEqual(shiftOfTwo), keyOfOne.Wrapped.Span.SequenceEqual(keyOfTwo.Wrapped.Span)));
        _ = Assert.Throws<EnvelopeRefusedException>(() => world.Content.Open(ContentColumns.DateShift, 1, shiftOfTwo, keyOfOne));
        Assert.Equal(first.Shift!.Days, (await world.DateShifts.EstablishAsync(1, Token)).Shift?.Days);
    }

    [DatabaseFact]
    public async Task DatabaseCopyHasNoReadableDateShift()
    {
        await using var world = await ContentWorld.CreateAsync();
        var shifts = new[] { await world.DateShifts.EstablishAsync(1, Token), await world.DateShifts.EstablishAsync(2, Token) };

        var dump = await world.Database.DumpAsync();

        // A column written in clear would be dumped as the bytes of the text, whole between the quotes; a bare number would be found by chance in any dump.
        Assert.Equal(
            (true, true, false, false),
            (dump.Contains(Hex("deslocamento-datas/1/v1"), StringComparison.Ordinal), dump.Contains(Hex("deslocamento-datas/2/v1"), StringComparison.Ordinal),
             dump.Contains($"'\\x{Hex(shifts[0].Shift!.Stored)}'", StringComparison.Ordinal), dump.Contains($"'\\x{Hex(shifts[1].Shift!.Stored)}'", StringComparison.Ordinal)));
    }

    [DatabaseFact]
    public async Task RotationOfTheEpochCarriesTheShiftToTheNewestEpoch()
    {
        await using var world = await ContentWorld.CreateAsync();
        var drawn = await world.DateShifts.EstablishAsync(1, Token);
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);
        using var onlyTheNewest = SyntheticCustody.Holding(2, ContentWorld.Labels);

        _ = await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(Token);

        var read = await new DateShifts(world.DataSource, onlyTheNewest.Content).EstablishAsync(1, Token);
        Assert.Equal((DateShiftOutcome.Kept, drawn.Shift!.Days), (read.Outcome, read.Shift?.Days));
    }

    [DatabaseFact]
    public async Task RewrapOfTheDomainCarriesTheShiftToTheNewestLabel()
    {
        await using var world = await ContentWorld.CreateAsync();
        await world.Database.RunAsOwnerAsync("INSERT INTO public.derivation_label (label) VALUES ('deslocamento-datas/1/v1'), ('deslocamento-datas/1/v2')");
        var drawn = await world.DateShifts.EstablishAsync(1, Token);
        using var custody = SyntheticCustody.Holding([.. ContentWorld.Labels, "deslocamento-datas/1/v2"]);

        var rewrapped = await new DomainRewrap(world.DataSource, 500).RewrapAsync(1, custody.Keys, custody.Labels, Token);

        var read = await new DateShifts(world.DataSource, custody.Content).EstablishAsync(1, Token);
        Assert.Equal(
            (1, "deslocamento-datas/1/v2", drawn.Shift!.Days),
            (rewrapped, EnvelopeCipher.LabelOf((byte[])(await world.InDomainAsync(1, Wrapped))!), read.Shift?.Days));
    }

    [DatabaseFact]
    public async Task ShiftThatDoesNotOpenIsLostAndNothingIsDrawnOverIt()
    {
        await using var world = await ContentWorld.CreateAsync();
        var drawn = await world.DateShifts.EstablishAsync(1, Token);
        using var withoutTheLabel = SyntheticCustody.Holding(ContentWorld.Labels.Where(label => !label.StartsWith("deslocamento-datas/", StringComparison.Ordinal)));

        var lost = await new DateShifts(world.DataSource, withoutTheLabel.Content).EstablishAsync(1, Token);

        Assert.Equal(
            (DateShiftOutcome.Lost, null, 1L, drawn.Shift!.Days),
            (lost.Outcome, lost.Shift, (long)(await world.OwnerInDomainAsync(1, Rows))!, (await world.DateShifts.EstablishAsync(1, Token)).Shift?.Days));
    }

    private static string Hex(string text) => Convert.ToHexStringLower(Encoding.UTF8.GetBytes(text));

    private static async Task<(byte[] Shift, ContentKey Key)> StoredAsync(ContentWorld world, int domainId)
    {
        var shift = (byte[])(await world.InDomainAsync(domainId, "SELECT shift FROM content.date_shift"))!;
        var epoch = (int)(await world.InDomainAsync(domainId, "SELECT dek_epoch FROM content.date_shift"))!;
        return (shift, new ContentKey(epoch, (byte[])(await world.InDomainAsync(domainId, Wrapped))!));
    }

    /// <summary>The first of the two draws, kept by the statement of the store in a transaction that does not commit yet.</summary>
    private static async Task KeepUncommittedAsync(ContentWorld world, ContentTransaction transaction, string days)
    {
        using var sealing = world.Content.Begin(ContentColumns.DateShiftKey, 1);
        var command = transaction.CreateCommand(ContentQueries.InsertDateShift);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.Add(new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = 1 });
            _ = command.Parameters.Add(new NpgsqlParameter("shift", NpgsqlDbType.Bytea) { Value = sealing.Seal(ContentColumns.DateShift, days).Envelope.ToArray() });
            _ = command.Parameters.Add(new NpgsqlParameter("epoch", NpgsqlDbType.Integer) { Value = sealing.Stored.Epoch });
            _ = command.Parameters.Add(new NpgsqlParameter("wrapped", NpgsqlDbType.Bytea) { Value = sealing.Stored.Wrapped.ToArray() });
            _ = await command.ExecuteNonQueryAsync(Token);
        }
    }

    private static async Task WaitForTheSecondToBlockAsync(ContentWorld world)
    {
        var server = await PostgresFixture.SharedAsync();
        await using var connection = new NpgsqlConnection(server.SuperuserConnectionString);
        await connection.OpenAsync(Token);
        var command = new NpgsqlCommand(WaitingToInsert, connection);
        await using (command.ConfigureAwait(false))
        {
            _ = command.Parameters.AddWithValue("database", new NpgsqlConnectionStringBuilder(world.Database.OwnerConnectionString).Database!);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
            while ((long)(await command.ExecuteScalarAsync(Token))! == 0)
            {
                Assert.True(DateTimeOffset.UtcNow < deadline, "The second draw did not wait for the first.");
                await Task.Delay(50, Token);
            }
        }
    }
}
