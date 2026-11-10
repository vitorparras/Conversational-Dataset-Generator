namespace Core.Host.Endpoints;

/// <summary>The source of grants of production before #112: no grant is recorded, so it gives none, and the screens show that, never an error.</summary>
internal sealed class NoDomainGrants : DomainGrants
{
    internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) => Task.FromResult(None);
}
