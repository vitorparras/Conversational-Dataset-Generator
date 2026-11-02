using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Custody;

/// <summary>The function that digests an event is the one the verification of the chain will use, so it refuses an input outside its contract and never returns a digest of something else.</summary>
[Trait("Category", "Database")]
public sealed class CustodyDigestContractTests : IClassFixture<CustodyDatabase>
{
    private const string Origin = "decode(repeat('00', 32), 'hex')";

    private readonly CustodyDatabase database;

    public CustodyDigestContractTests(CustodyDatabase database)
    {
        this.database = database;
    }

    [DatabaseFact]
    public async Task InputWithinTheContractGivesADigestOfThirtyTwoBytes() =>
        Assert.Equal(32L, await database.CountAsOwnerAsync($"SELECT octet_length({Call(Origin, "false", "'person'")})::bigint"));

    [DatabaseFact]
    public async Task PreviousDigestThatIsNotThirtyTwoBytesIsRefused()
    {
        var refused = await database.RefusedToOwnerAsync($"SELECT {Call("decode('00', 'hex')", "false", "'person'")}");

        Assert.Equal(PostgresErrorCodes.InvalidParameterValue, refused.SqlState);
    }

    [DatabaseFact]
    public async Task UnknownWhetherTheSystemSignsIsRefusedAndNotTakenAsNo()
    {
        var refused = await database.RefusedToOwnerAsync($"SELECT {Call(Origin, "NULL", "'person'")}");

        Assert.Equal(PostgresErrorCodes.InvalidParameterValue, refused.SqlState);
    }

    [DatabaseFact]
    public async Task AbsentTextIsRefusedAndNotDigestedAsNothing()
    {
        var refused = await database.RefusedToOwnerAsync($"SELECT {Call(Origin, "false", "NULL")}");

        Assert.Equal(PostgresErrorCodes.InvalidParameterValue, refused.SqlState);
    }

    private static string Call(string previous, string system, string kind) =>
        $"public.custody_digest({previous}, 1, '2026-01-01 00:00:00+00', NULL, {system}, {kind}, 1, 'person_created', 'administrative_act', NULL, 'app')";
}
