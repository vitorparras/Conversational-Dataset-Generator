using Core.Host.Screens;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Domains;
using Npgsql;

namespace Core.Host.Endpoints;

/// <summary>
/// What the screens of the rerun of the sanitization (issue 97) ask of the server, in one place: the identifier of a domain by its name, the items by version, the request in the name of the person
/// of the session to the steps in force, and where a request stands, with its phase at the moment of the clock. The queue is the one of <see cref="SanitizationReruns"/>; it reads no text.
/// </summary>
internal sealed class RerunDesk
{
    private readonly DomainListing domains;
    private readonly SanitizationReruns queue;
    private readonly TimeProvider time;
    private readonly string listed;

    private RerunDesk(NpgsqlDataSource source, TimeProvider time, string version, string listed)
    {
        domains = new DomainListing(source);
        queue = new SanitizationReruns(source, time);
        this.time = time;
        Version = version;
        this.listed = listed;
    }

    /// <summary>The version of the steps in force in this process, the one every request reruns to.</summary>
    internal string Version { get; }

    /// <summary>The desk of the root; none when no database was declared, and the screens then say it is the host that cannot.</summary>
    /// <param name="provider">The container of the host.</param>
    internal static RerunDesk Of(IServiceProvider provider)
    {
        var steps = CompositionRoot.StepsOf();
        return provider.GetRequiredService<ServiceProvider>().GetService<NpgsqlDataSource>() is { } source
            ? new RerunDesk(source, provider.GetRequiredService<TimeProvider>(), steps.Version, string.Join('\n', steps.Order.Select(step => $"{step.Name}@{step.Version}")))
            : null!;
    }

    /// <summary>The identifier of the domain with the name given, as the active domain of the person names it; none when no domain has the name.</summary>
    internal async Task<int?> DomainIdOfAsync(string name, CancellationToken cancellationToken)
    {
        var found = await GrantedDomains.OfAsync(domains, Enumerable.Repeat(name, 1), cancellationToken).ConfigureAwait(false);
        return found.Keys.Select(id => (int?)id).FirstOrDefault();
    }

    internal Task<RerunSituation> SituationAsync(int domainId, CancellationToken cancellationToken) => queue.SituationAsync(domainId, cancellationToken);

    /// <summary>Queues the items of the domain that are in another version, in the name of the person of the session, to the steps in force.</summary>
    internal Task<RerunResult> RequestAsync(int domainId, long entered, CancellationToken cancellationToken) =>
        queue.RequestAsync(new RerunRequest(domainId, entered, Version, listed), cancellationToken);

    /// <summary>Where the request stands and its phase now; none when the domain has no such request.</summary>
    internal async Task<Followed?> ProgressAsync(int domainId, long rerunId, CancellationToken cancellationToken) =>
        await queue.ProgressAsync(domainId, rerunId, cancellationToken).ConfigureAwait(false) is { } progress ? new Followed(progress, PhaseOf(progress)) : null;

    /// <summary>
    /// A request whose items another request closed is superseded; with nothing waiting it is done, whatever version it reran to; still open to another version it is superseded; with no
    /// item concluded within a hold, counted from the last one concluded or, with none, from the request, it is stopped; with nothing concluded nor held it is queued, and otherwise running.
    /// </summary>
    private RerunProgressScreen.Phase PhaseOf(RerunProgress progress)
    {
        var since = time.GetUtcNow() - (progress.LastConcludedAt ?? progress.RequestedAt);
        return (progress.Superseded > 0, progress.Queued + progress.Held == 0, progress.TargetVersion != Version, since > SanitizationReruns.Hold) switch
        {
            (true, _, _, _) => RerunProgressScreen.Phase.Superseded,
            (_, true, _, _) => RerunProgressScreen.Phase.Done,
            (_, _, true, _) => RerunProgressScreen.Phase.Superseded,
            (_, _, _, true) => RerunProgressScreen.Phase.Stopped,
            _ when progress.Held + progress.Identical + progress.Replaced + progress.Refused == 0 => RerunProgressScreen.Phase.Queued,
            _ => RerunProgressScreen.Phase.Running,
        };
    }

    /// <summary>Where a request stands and the phase it is in.</summary>
    internal sealed record Followed(RerunProgress Progress, RerunProgressScreen.Phase Phase);
}
