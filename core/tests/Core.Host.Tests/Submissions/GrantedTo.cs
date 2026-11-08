using Core.Host.Endpoints;

namespace Core.Host.Tests.Submissions;

/// <summary>The domains granted to whoever asks, fixed by the test, since no grant is recorded before #112.</summary>
internal sealed class GrantedTo : IGrantedDomains
{
    private readonly int[] domains;

    internal GrantedTo(params int[] domains)
    {
        this.domains = domains;
    }

    public Task<IReadOnlyCollection<int>> OfAsync(long requesterId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<int>>(domains);
}
