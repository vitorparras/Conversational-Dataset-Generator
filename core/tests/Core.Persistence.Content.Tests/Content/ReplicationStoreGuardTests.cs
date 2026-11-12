using Core.Persistence.Content;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>The guard of the replication store: a source that is missing is refused at the door, and a source that is there is the one the store reads through.</summary>
[Trait("Category", "Database")]
public sealed class ReplicationStoreGuardTests
{
    [Fact]
    public void ASourceThatIsMissingIsRefusedByTheDataSourceParameter()
    {
        var refused = Assert.Throws<ArgumentNullException>(() => new ReplicationStore(null!));

        Assert.Equal("dataSource", refused.ParamName);
    }

    [Fact]
    public async Task TheStoreReadsThroughTheSourceItWasGiven()
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");
        var store = new ReplicationStore(dataSource);

        _ = await Assert.ThrowsAnyAsync<NpgsqlException>(() => store.OccurrencesAsync(1, ["a"], CancellationToken.None));
        _ = await Assert.ThrowsAnyAsync<NpgsqlException>(() => store.MarksAsync(1, ["a"], CancellationToken.None));
    }
}
