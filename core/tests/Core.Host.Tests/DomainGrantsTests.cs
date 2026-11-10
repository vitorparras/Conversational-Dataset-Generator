using Core.Host.Endpoints;

namespace Core.Host.Tests;

/// <summary>The source of the domains granted (#617): the one of production before #112 gives none to anybody, and a test host can put its own in its place.</summary>
public sealed class DomainGrantsTests
{
    [Theory]
    [InlineData(1L)]
    [InlineData(9_000_000L)]
    public async Task TheSourceOfProductionGrantsNoDomainToAnyone(long person)
    {
        DomainGrants grants = new NoDomainGrants();

        var granted = await grants.GrantedToAsync(person, TestContext.Current.CancellationToken);

        Assert.Empty(granted);
    }

    [Fact]
    public async Task ASourceThatATestHostRegistersGrantsWhatItWasFilledWithAndOnlyToWhomItWasFilledFor()
    {
        DomainGrants grants = new FilledGrants(7L, "Domínio A", "Domínio B");

        var forHim = await grants.GrantedToAsync(7L, TestContext.Current.CancellationToken);
        var forOther = await grants.GrantedToAsync(8L, TestContext.Current.CancellationToken);

        Assert.Equal(["Domínio A", "Domínio B"], forHim);
        Assert.Empty(forOther);
    }

    private sealed class FilledGrants : DomainGrants
    {
        private readonly long person;
        private readonly string[] domains;

        internal FilledGrants(long person, params string[] domains)
        {
            this.person = person;
            this.domains = domains;
        }

        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult(entered == person ? domains.AsEnumerable() : None);
    }
}
