using System.Globalization;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Tests.Custody;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.Verification;

/// <summary>
/// The anchor of the verification (#61, CA-4): synthetic events whose digests were worked out apart from this repository, from the encoding the migration 0008 declares, byte by byte.
/// The digest written in C# for the verification and the function the database writes the links with must both give these answers; when one does not, the message says which of the two it is.
/// </summary>
[Trait("Category", "Database")]
public sealed class ChainDigestVectorTests : IClassFixture<CustodyDatabase>
{
    private const string Origin = "0000000000000000000000000000000000000000000000000000000000000000";

    // Each vector points to the digest of the one before it; the first points to the origin. They cover: the epoch itself; a moment with microseconds; a moment before the epoch
    // (negative); an item beyond what a double keeps (2^53 + 1); an author that is a person and one that is the system; an absent number against the number zero; the largest sequence
    // and item; a text of several bytes per character; and every step and reason the chain lists besides the first ones.
    private static readonly Vector[] Vectors =
    [
        new("epoch, system, no correction", Origin, 1, "2000-01-01T00:00:00Z", 0, null, true, "person", 1, "person_created", "administrative_act", null, "app",
            "f7b85a261d0e6d11c97e70dd48ae2b92e1037610bafbd546444fc5c01b453608"),
        new("person author, microseconds, item beyond 2^53", "f7b85a261d0e6d11c97e70dd48ae2b92e1037610bafbd546444fc5c01b453608", 2, "2031-03-04T05:06:07.123456Z", 983_682_367_123_456, 42, false,
            "item_text", 9_007_199_254_740_993, "raw_text_read", "content_read", null, "zqx-leitor",
            "a24fa5a900af73d90515e6a0105a5366d8662188fbd93ae29ee27aa2cf621c97"),
        new("moment before the epoch, correction", "a24fa5a900af73d90515e6a0105a5366d8662188fbd93ae29ee27aa2cf621c97", 3, "1999-12-31T23:59:59.999999Z", -1, 1, false,
            "person", 7, "correction", "correction_of_record", 2, "owner",
            "cc78a08aa1fdba76ed47842f965d5d94dd6d7061ef33dbf6ffea0f84cfd03ad3"),
        new("verification of the chain, text of several bytes per character", "cc78a08aa1fdba76ed47842f965d5d94dd6d7061ef33dbf6ffea0f84cfd03ad3", 4, "2026-10-31T13:00:00.000001Z", 846_766_800_000_001,
            null, true, "custody_chain", 0, "verify_chain", "verification_failed", 1, "ação-ü€",
            "c8c339c340e23327ae26b511c245c841e864bfaf39375f79e04ecdfed6b500df"),
        new("largest sequence and item, zero present and not absent", "c8c339c340e23327ae26b511c245c841e864bfaf39375f79e04ecdfed6b500df", long.MaxValue, "2262-04-11T23:47:16.854775Z",
            8_276_687_236_854_775, 0, false, "person", long.MaxValue, "person_password_set", "administrative_act", 0, "app",
            "542ec8db5928820bed7ddb7f44eb5c8b8a4acf5b5df4b1f3ba9efb37270ad18f"),
    ];

    private readonly CustodyDatabase database;

    public ChainDigestVectorTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void DigestWrittenInCSharpForTheVerificationIsTheKnownAnswer(int index)
    {
        var vector = Vectors[index];

        var digest = ChainDigest.Of(vector.Stored(), Convert.FromHexString(vector.Previous));

        Assert.True(vector.Digest == Convert.ToHexStringLower(digest), $"ChainDigest diverges from the known answer of the vector '{vector.Name}'.");
    }

    [DatabaseTheory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task FunctionTheDatabaseWritesTheLinksWithIsTheKnownAnswer(int index)
    {
        var vector = Vectors[index];

        var digest = await database.TextAsOwnerAsync(
            "SELECT pg_catalog.encode(public.custody_digest(pg_catalog.decode(@previous, 'hex'), @sequence::bigint, @moment::timestamptz, @person::bigint, @system::boolean, "
            + "@kind::text, @item::bigint, @step::text, @reason::text, @corrects::bigint, @by::text), 'hex')",
            ("previous", vector.Previous),
            ("sequence", vector.Sequence),
            ("moment", vector.Moment),
            ("person", Nullable(vector.Person)),
            ("system", vector.System),
            ("kind", vector.Kind),
            ("item", vector.Item),
            ("step", vector.Step),
            ("reason", vector.Reason),
            ("corrects", Nullable(vector.Corrects)),
            ("by", vector.RecordedBy));

        Assert.True(vector.Digest == digest, $"custody_digest of the database diverges from the known answer of the vector '{vector.Name}'.");
    }

    [Fact]
    public void MicrosecondsOfTheVectorsAreTheOnesTheirMomentsMeanSinceTheEpoch()
    {
        var epoch = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        foreach (var vector in Vectors)
        {
            var moment = DateTimeOffset.Parse(vector.Moment, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
            Assert.True((moment - epoch).Ticks / 10 == vector.Microseconds, $"The microseconds of the vector '{vector.Name}' are not those of its moment.");
        }
    }

    [Fact]
    public void ChainOfTheVectorsIsLinkedEachPointingToTheDigestOfTheOneBefore()
    {
        Assert.Equal(Origin, Vectors[0].Previous);
        for (var index = 1; index < Vectors.Length; index++)
        {
            Assert.Equal(Vectors[index - 1].Digest, Vectors[index].Previous);
        }
    }

    [Fact]
    public void AnyChangeToAFieldOfAVectorChangesItsDigest()
    {
        var vector = Vectors[1];
        var original = Convert.ToHexStringLower(ChainDigest.Of(vector.Stored(), Convert.FromHexString(vector.Previous)));
        StoredEvent[] changed =
        [
            vector.Stored() with { Sequence = vector.Sequence + 1 },
            vector.Stored() with { OccurredAtMicroseconds = vector.Microseconds + 1 },
            vector.Stored() with { ActorPersonId = 43 },
            vector.Stored() with { ActorSystem = true },
            vector.Stored() with { ItemKind = "person" },
            vector.Stored() with { ItemId = vector.Item - 1 },
            vector.Stored() with { Step = "sanitized_text_read" },
            vector.Stored() with { Reason = "administrative_act" },
            vector.Stored() with { Corrects = 1 },
            vector.Stored() with { RecordedBy = "app" },
        ];

        var digests = changed.Select(stored => Convert.ToHexStringLower(ChainDigest.Of(stored, Convert.FromHexString(vector.Previous)))).Append(original);

        Assert.Equal(changed.Length + 1, digests.Distinct().Count());
    }

    private static object Nullable(long? value) => value.HasValue ? value.Value : DBNull.Value;

    private sealed record Vector(
        string Name, string Previous, long Sequence, string Moment, long Microseconds, long? Person, bool System, string Kind, long Item,
        string Step, string Reason, long? Corrects, string RecordedBy, string Digest)
    {
        public StoredEvent Stored() => new(Sequence, Microseconds, Person, System, Kind, Item, Step, Reason, Corrects, [], [], RecordedBy);
    }
}
