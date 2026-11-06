using System.Data;
using Core.Persistence.Content;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>The guards of the other ways to open a transaction of content, and of the change of its domain (issue 683): each refusal names the parameter and holds the value it refused.</summary>
[Trait("Category", "Database")]
public sealed class ContentTransactionGuardTests
{
    [Fact]
    public async Task OpeningAtAnIsolationWithASourceThatIsMissingIsRefusedByTheDataSourceParameter()
    {
        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => ContentTransaction.BeginAsync(null!, 1, IsolationLevel.Serializable, CancellationToken.None));

        Assert.Equal("dataSource", refused.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task OpeningAtAnIsolationWithADomainThatIsNotPositiveIsRefusedByTheDomainParameterWithoutOpeningAConnection(int domainId)
    {
        using var dataSource = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d");

        var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ContentTransaction.BeginAsync(dataSource, domainId, IsolationLevel.Serializable, CancellationToken.None));

        Assert.Equal(("domainId", domainId), (refused.ParamName, refused.ActualValue));
    }

    [Fact]
    public async Task OpeningAtAnIsolationTellsTheSourceBeforeTheDomain()
    {
        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => ContentTransaction.BeginAsync(null!, 0, IsolationLevel.Serializable, CancellationToken.None));

        Assert.Equal("dataSource", refused.ParamName);
    }

    [Fact]
    public async Task TheSnapshotWithASourceThatIsMissingIsRefusedByTheDataSourceParameter()
    {
        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => ContentTransaction.BeginSnapshotAsync(null!, IsolationLevel.RepeatableRead, CancellationToken.None));

        Assert.Equal("dataSource", refused.ParamName);
    }

    [DatabaseTheory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task ChangingToADomainThatIsNotPositiveIsRefusedByTheDomainParameterAndLeavesTheDomainDeclared(int domainId)
    {
        await using var world = await ContentWorld.CreateAsync();
        var transaction = await ContentTransaction.BeginAsync(world.DataSource, 1, TestContext.Current.CancellationToken);
        await using (transaction.ConfigureAwait(false))
        {
            var refused = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => transaction.ChangeDomainAsync(domainId, TestContext.Current.CancellationToken));

            var command = transaction.CreateCommand("SELECT public.declared_domain()");
            await using (command.ConfigureAwait(false))
            {
                Assert.Equal(("domainId", domainId, 1), (refused.ParamName, refused.ActualValue, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
            }
        }
    }
}
