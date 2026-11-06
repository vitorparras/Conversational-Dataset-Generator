using Core.Persistence.Content;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>The guards of the opening in the scope of the system (issue 74): a source missing or a domain that is not positive is refused by the parameter, before any connection opens.</summary>
public sealed class ContentSystemTransactionGuardTests
{
    [Fact]
    public async Task ASourceThatIsMissingIsRefusedByTheDataSourceParameter()
    {
        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => ContentTransaction.BeginSystemAsync(null!, 1, CancellationToken.None));

        Assert.Equal("dataSource", refused.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task ADomainThatIsNotPositiveIsRefusedByTheDomainParameterWithoutOpeningAConnection(int domainId)
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");

        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ContentTransaction.BeginSystemAsync(dataSource, domainId, CancellationToken.None));

        Assert.Equal(("domainId", domainId), (refused.ParamName, refused.ActualValue));
    }

    [Fact]
    public async Task ASourceThatIsMissingIsToldBeforeADomainThatIsNotPositive()
    {
        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => ContentTransaction.BeginSystemAsync(null!, 0, CancellationToken.None));

        Assert.Equal("dataSource", refused.ParamName);
    }
}
