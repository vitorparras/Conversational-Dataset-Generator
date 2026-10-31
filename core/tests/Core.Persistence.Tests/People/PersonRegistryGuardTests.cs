using Core.Persistence.Custody;
using Core.Persistence.People;
using Core.Persistence.Tests.ServerGuarantees;

namespace Core.Persistence.Tests.People;

/// <summary>What the registry and the store of passwords refuse when something they need is not given: an error that names it, before any connection is opened.</summary>
public sealed class PersonRegistryGuardTests
{
    private static readonly NewPerson Someone = new("zqx-pessoa-1", PersonFunctions.Accepted[0]);

    [Fact]
    public void RegistryWithoutADataSourceIsRefused() =>
        Assert.Equal("dataSource", Assert.Throws<ArgumentNullException>(() => new PersonRegistry(null!)).ParamName);

    [Fact]
    public async Task CreatingWithoutAPersonIsRefused()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.CreateAsync(null!, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal("person", refused.ParamName);
    }

    [Fact]
    public async Task CreatingWithoutAnAuthorIsRefused()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.CreateAsync(Someone, null!, TestContext.Current.CancellationToken));

        Assert.Equal("author", refused.ParamName);
    }

    [Fact]
    public async Task DeactivatingWithoutAnAuthorIsRefused()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.DeactivateAsync(1, null!, TestContext.Current.CancellationToken));

        Assert.Equal("author", refused.ParamName);
    }

    [Fact]
    public async Task ChangingTheFunctionWithoutOneIsRefused()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.ChangeFunctionAsync(1, null!, CustodyAuthor.System, TestContext.Current.CancellationToken));

        Assert.Equal("function", refused.ParamName);
    }

    [Fact]
    public async Task ChangingTheFunctionWithoutAnAuthorIsRefused()
    {
        await using var server = new UnreachableServer();
        var registry = new PersonRegistry(server.DataSource);

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => registry.ChangeFunctionAsync(1, PersonFunctions.Accepted[1], null!, TestContext.Current.CancellationToken));

        Assert.Equal("author", refused.ParamName);
    }

    [Fact]
    public async Task SettingAPasswordWithoutAnAuthorIsRefused()
    {
        await using var server = new UnreachableServer();
        var store = new PersonCredentials(server.DataSource, new PasswordDerivation(1));

        var refused = await Assert.ThrowsAsync<ArgumentNullException>(() => store.SetPasswordAsync(1, "ZQX-SENHA-9053", null!, TestContext.Current.CancellationToken));

        Assert.Equal("author", refused.ParamName);
    }
}
