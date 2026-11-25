using Core.Persistence.Ingestion;
using Npgsql;

namespace Core.Persistence.Tests.Ingestion;

/// <summary>The reviews refuse, by name and before any connection, a missing source, a sample declared with no item and a domain that is not one.</summary>
public sealed class BatchReviewsArgumentsTests
{
    [Fact]
    public async Task MissingSourceEmptySampleAndNoDomainAreRefusedByName()
    {
        await using var source = NpgsqlDataSource.Create("Host=zqx.invalid");

        var withoutSource = Assert.Throws<ArgumentNullException>(() => new BatchReviews(null!, 1));
        var emptySample = Assert.Throws<ArgumentOutOfRangeException>(() => new BatchReviews(source, 0));
        var noDomain = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => new BatchReviews(source, null).ReadAsync(0, 1, TestContext.Current.CancellationToken));

        Assert.Equal(("dataSource", "sampleSize", "domainId"), (withoutSource.ParamName, emptySample.ParamName, noDomain.ParamName));
        Assert.StartsWith("The size of the sample is positive when declared.", emptySample.Message, StringComparison.Ordinal);
    }
}
