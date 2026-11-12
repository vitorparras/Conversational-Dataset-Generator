using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>
/// The batch that accepted a file before (issue 80, CA-2), as the register finds it: only an accepted batch of the domain asked, by the digest of the file.
/// </summary>
public sealed class BatchPreviousTests
{
    private const int Other = IngestionWorld.Domain + 1;

    [DatabaseFact]
    public async Task OnlyTheAcceptedBatchOfTheDomainWithTheDigestIsThePreviousOne()
    {
        await using var world = await IngestionWorld.CreateAsync();
        var token = TestContext.Current.CancellationToken;
        var accepted = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'a');
        _ = await IngestionWorld.AcceptAsync(world, accepted);
        _ = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'b');
        var rejected = await IngestionWorld.OpenFileAsync(world, IngestionWorld.Domain, 'c');
        _ = await IngestionWorld.RejectAsync(world, rejected, "too_large");
        var elsewhere = await IngestionWorld.OpenFileAsync(world, Other, 'd');
        _ = await IngestionWorld.AcceptInAsync(world, Other, elsewhere);
        var register = new BatchRegister(world.DataSource);

        Assert.Equal(
            [accepted, null, null, null, null, elsewhere],
            [
                await register.PreviousAsync(IngestionWorld.Domain, Digest('a'), token),
                await register.PreviousAsync(IngestionWorld.Domain, Digest('b'), token),
                await register.PreviousAsync(IngestionWorld.Domain, Digest('c'), token),
                await register.PreviousAsync(IngestionWorld.Domain, Digest('e'), token),
                await register.PreviousAsync(IngestionWorld.Domain, Digest('d'), token),
                await register.PreviousAsync(Other, Digest('d'), token),
            ]);
    }

    [Fact]
    public void ADigestThatIsMissingOrEmptyIsRefusedByName()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var register = new BatchRegister(dataSource);

        Assert.Equal(
            ["digest", "digest"],
            [
                Param(() => register.PreviousAsync(1, null!, CancellationToken.None)),
                Param(() => register.PreviousAsync(1, string.Empty, CancellationToken.None)),
            ]);
    }

    private static string Digest(char digit) => new(digit, 64);

    private static string? Param(Func<object?> call) => Assert.ThrowsAny<ArgumentException>(call).ParamName;
}
