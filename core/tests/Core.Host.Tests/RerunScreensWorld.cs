using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Core.Boundary.Text;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Submissions;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>
/// The screens of the rerun (issue 97) behind the real gates, over the world of the worker: one accepted batch of two items sanitized by a list of steps older than the one in force, a person of the
/// curation and the person of the annotation that submitted it, both granted the domain, and the host reading the clock of the world, with the desk the root registers and no worker of its own.
/// </summary>
internal sealed partial class RerunScreensWorld : IAsyncDisposable
{
    private RerunScreensWorld(RerunWorld rerun, SessionedHost host, (long Curator, long OtherCurator) people)
    {
        Rerun = rerun;
        Host = host;
        (Curator, OtherCurator) = people;
    }

    internal RerunWorld Rerun { get; }

    internal SessionedHost Host { get; }

    /// <summary>The person of the curation granted the domain of the batch.</summary>
    internal long Curator { get; }

    /// <summary>A person of the curation granted only the other domain, which has no request of its own.</summary>
    internal long OtherCurator { get; }

    /// <summary>The person of the annotation, who submitted the batch and is granted its domain, but whose function reaches no screen of the rerun.</summary>
    internal static long Annotator => SubmissionWorld.Person;

    /// <summary>The rerun of the steps in force, the one the host requests to.</summary>
    internal static SanitizationRerun InForce => new(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries()));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [SuppressMessage("Reliability", "CA2000", Justification = "The world takes the rerun and the host it is given and lets go of them with itself.")]
    internal static async Task<RerunScreensWorld> StartAsync()
    {
        var rerun = await RerunWorld.CreateAsync();
        var database = rerun.Submissions.Database;
        await database.RunAsOwnerAsync("INSERT INTO public.person (name, function_name) VALUES ('zqx-curadora', 'Curadoria do acervo'), ('zqx-curadora-b', 'Curadoria do acervo')");
        var curator = long.Parse((await database.FirstColumnAsOwnerAsync("SELECT id::text FROM public.person WHERE name = 'zqx-curadora'"))[0], CultureInfo.InvariantCulture);
        var otherCurator = long.Parse((await database.FirstColumnAsOwnerAsync("SELECT id::text FROM public.person WHERE name = 'zqx-curadora-b'"))[0], CultureInfo.InvariantCulture);
        var source = rerun.Submissions.DataSource;
        var host = await SessionedHost.StartAsync(
            services => services
                .AddSingleton(new ServiceCollection().AddSingleton(source).BuildServiceProvider())
                .AddSingleton<TimeProvider>(rerun.Clock)
                .AddSingleton(provider => new OperatorAdministration(
                    new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), provider.GetRequiredService<SessionStore>(), new PersonNotices(source)))
                .AddSingleton(new BatchRegister(source)).AddSingleton(new BatchCounts(source)).AddSingleton(new DomainListing(source))
                .AddSingleton<DomainGrants>(new GrantedTo((SubmissionWorld.Domain, [curator, Annotator]), (SubmissionWorld.OtherDomain, [otherCurator]))),
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                BatchRoutes.Map(routes);
            },
            Token);
        return new RerunScreensWorld(rerun, host, (curator, otherCurator));
    }

    /// <summary>The page of an address as the person sees it, with whatever status the host answered.</summary>
    internal async Task<Reply> GetAsync(string route, long? person = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        request.Headers.Accept.ParseAdd(SampleReviewWorld.Browser);
        using var response = await Host.SendAsync(request, Host.Sessions.Open(person ?? Curator), encrypted: true, Token);
        return new Reply(response.StatusCode, response.Headers.Location?.OriginalString, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)));
    }

    /// <summary>What the confirmation of the rerun, posted by the person, is answered with.</summary>
    internal async Task<Reply> ConfirmAsync(long? person = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ScreenPaths.RerunConfirm, UriKind.Relative)) { Content = new FormUrlEncodedContent([]) };
        request.Headers.Accept.ParseAdd(SampleReviewWorld.Browser);
        using var response = await Host.SendAsync(request, Host.Sessions.Open(person ?? Curator), encrypted: true, Token);
        return new Reply(response.StatusCode, response.Headers.Location?.OriginalString, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)));
    }

    /// <summary>The items by version and the open request, read by the queue of production.</summary>
    internal Task<RerunSituation> SituationAsync() => Rerun.Queue.SituationAsync(SubmissionWorld.Domain, Token);

    /// <summary>The phase the progress of the request says, as the person reads it.</summary>
    internal async Task<string> PhaseAsync(long rerun) => PhaseShown().Match((await GetAsync(ScreenPaths.RerunProgressOf(rerun))).Page).Groups[1].Value;

    /// <summary>Holds the next item of the queue and concludes it by the steps in force, as the worker would, at the moment of the clock.</summary>
    internal async Task ConcludeOneAsync()
    {
        var hold = await Rerun.Queue.TakeAsync(SubmissionWorld.Domain, CompositionRoot.StepsOf().Version, Token) ?? throw new InvalidOperationException("No item was queued.");
        var resanitizer = new ItemResanitizer(Rerun.Submissions.DataSource, Rerun.Submissions.Content, Rerun.Clock);
        _ = await resanitizer.ResanitizeAsync(hold, held => DecidedItemTexts.Rerun(InForce, CompositionRoot.SwapOf(), held), Token);
    }

    /// <summary>Spends every try of each item left in the queue, as a worker that keeps failing would, so that each is refused and the request ends.</summary>
    internal async Task RefuseTheRestAsync()
    {
        while (await Rerun.Queue.TakeAsync(SubmissionWorld.Domain, CompositionRoot.StepsOf().Version, Token) is { } hold)
        {
            _ = await Rerun.Queue.FailAsync(hold, Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await Rerun.DisposeAsync();
    }

    [GeneratedRegex("<dt>Situação</dt>\\s*<dd>([^<]*)</dd>", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PhaseShown();

    private sealed class GrantedTo(params (int Domain, long[] People)[] grants) : DomainGrants
    {
        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<string>>([.. grants.Where(grant => grant.People.Contains(entered)).Select(grant => SubmissionWorld.NameOf(grant.Domain))]);
    }
}
