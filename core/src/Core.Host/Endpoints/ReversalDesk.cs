using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Domains;
using Core.Persistence.People;
using Core.Platform.Keys;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// What the screens of the request to reverse a marker and of its authorization (issues 614 and 615) ask of the server, in one place: the identifier of a domain by its name, the name of a
/// person, and the requests, opened with the master key read at each access, as the review of the sample reads it, so that a key that stops being readable stops the screens and no other.
/// </summary>
internal sealed class ReversalDesk
{
    private const string Removed = "Pessoa removida";

    private readonly IServiceProvider startup;
    private readonly NpgsqlDataSource source;
    private readonly DomainListing domains;
    private readonly PersonRegistry people;

    private ReversalDesk(IServiceProvider startup, NpgsqlDataSource source, TimeProvider time)
    {
        this.startup = startup;
        this.source = source;
        domains = new DomainListing(source);
        people = new PersonRegistry(source);
        Time = time;
    }

    /// <summary>The clock of the host, which says the day an authorization is offered until.</summary>
    internal TimeProvider Time { get; }

    /// <summary>The desk of the root; none when no database was declared, and the screens then say it is the host that cannot.</summary>
    /// <param name="provider">The container of the host.</param>
    internal static ReversalDesk Of(IServiceProvider provider)
    {
        var startup = provider.GetRequiredService<ServiceProvider>();
        return startup.GetService<NpgsqlDataSource>() is { } source ? new ReversalDesk(startup, source, provider.GetRequiredService<TimeProvider>()) : null!;
    }

    /// <summary>The identifier of the domain with the name given, as the active domain of the person names it; none when no domain has the name.</summary>
    internal async Task<int?> DomainIdOfAsync(string name, CancellationToken cancellationToken)
    {
        var found = await GrantedDomains.OfAsync(domains, Enumerable.Repeat(name, 1), cancellationToken).ConfigureAwait(false);
        return found.Keys.Select(id => (int?)id).FirstOrDefault();
    }

    /// <summary>The name of the person, or that the person was removed.</summary>
    internal async Task<string> NameOfAsync(long person, CancellationToken cancellationToken) =>
        (await people.NamesOfAsync([person], cancellationToken).ConfigureAwait(false)).TryGetValue(person, out var name) ? name : Removed;

    /// <summary>The requests over the key read now; none when the key cannot be read, and the screen then says the host cannot.</summary>
    internal async Task<ReversalAccess?> OpenAsync(CancellationToken cancellationToken)
    {
        var reading = await startup.GetRequiredService<IMasterKeySource>().ReadAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (reading.Failure is not null)
            {
                reading.Dispose();
                return null;
            }

            var content = CompositionRoot.ContentOf(reading, await RegisteredLabels.ReadAsync(source, cancellationToken).ConfigureAwait(false));
            var reader = new ReversalRequestReader(source, new ProtectedContentReader(source, content, new CustodyChain(source)), Time);
            return new ReversalAccess(new ReversalRequests(source, content, Time), reader, reading);
        }
        catch
        {
            reading.Dispose();
            throw;
        }
    }
}
