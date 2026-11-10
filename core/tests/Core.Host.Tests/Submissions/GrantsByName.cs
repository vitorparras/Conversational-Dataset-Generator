using Core.Host.Endpoints;

namespace Core.Host.Tests.Submissions;

/// <summary>The grants of whoever asks, fixed by the test as the names of the domains, since no grant is recorded before #112.</summary>
internal sealed class GrantsByName : DomainGrants
{
    private readonly string[] names;

    internal GrantsByName(params string[] names)
    {
        this.names = names;
    }

    internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) => Task.FromResult(names.AsEnumerable());
}
