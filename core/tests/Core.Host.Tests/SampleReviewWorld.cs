using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Review;
using Core.Host.Submissions;
using Core.Host.Tests.Submissions;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The review of the sample (issue 822) behind the real gates over a database of its own: a batch left open by the submission of a conversation, one item per message,
/// a person of the curation granted the domain of the batch and another of the curation granted nothing, and the person of the annotation that submitted it.
/// </summary>
internal sealed class SampleReviewWorld : IAsyncDisposable
{
    internal const string Browser = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8";

    private SampleReviewWorld(SubmissionWorld submissions, SessionedHost host, (long Batch, long Curator, long Stranger) ids, (int? Size, int Messages, NpgsqlDataSource Desk) shape)
    {
        DeskSource = shape.Desk;
        Submissions = submissions;
        Host = host;
        Batch = ids.Batch;
        Curator = ids.Curator;
        Stranger = ids.Stranger;
        Reviews = new BatchReviews(submissions.DataSource, shape.Size);
        Messages = shape.Messages;
    }

    internal SessionedHost Host { get; }

    internal long Batch { get; }

    /// <summary>The person of the curation granted the domain of the batch.</summary>
    internal long Curator { get; }

    /// <summary>A person of the curation that no domain is granted to.</summary>
    internal long Stranger { get; }

    /// <summary>The person of the annotation, who submitted the batch and whose function reaches no screen of the review.</summary>
    internal static long Annotator => SubmissionWorld.Person;

    internal BatchReviews Reviews { get; }

    internal SubmissionWorld Submissions { get; }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private int Messages { get; }

    /// <summary>The connection of the desk and of the register of the batches, apart from the one of the gates so that losing it leaves the person signed in.</summary>
    private NpgsqlDataSource DeskSource { get; }

    /// <summary>Takes the database away from the desk and from the register, as a server that stops answering does, while the session of the person still holds.</summary>
    internal ValueTask LoseDatabaseAsync() => DeskSource.DisposeAsync();

    /// <summary>A world with the batch open and its sample not drawn yet.</summary>
    /// <param name="size">The size of the sample the installation declares; none when it declares none.</param>
    /// <param name="messages">How many messages the conversation of the batch has, which is how many items.</param>
    [SuppressMessage("Reliability", "CA2000", Justification = "The world takes the submissions and the host it is given and lets go of them with itself.")]
    internal static async Task<SampleReviewWorld> StartAsync(int? size = 2, int messages = 4)
    {
        var submissions = await SubmissionWorld.CreateAsync();
        await submissions.Database.RunAsOwnerAsync(
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-curadora', 'Curadoria do acervo'), ('zqx-curadora-sem-concessao', 'Curadoria do acervo')");
        var curator = await PersonAsync(submissions, "zqx-curadora");
        var stranger = await PersonAsync(submissions, "zqx-curadora-sem-concessao");
        var outcome = await submissions.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(Entries(messages))));
        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        var source = submissions.DataSource;
        var staff = new OperatorAdministration(
            new PersonRegistry(source),
            new PersonCredentials(source, new PasswordDerivation(1)),
            new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System),
            new PersonNotices(source));
        var held = NpgsqlDataSource.Create(submissions.Database.AppConnectionString);
        var desk = new SampleReviewDesk(
            (new BatchReviews(held, size), new SampleMarks(held), new BatchApprovals(held)),
            (new PersonRegistry(held), _ => Task.FromResult(new SampleTextAccess(new SampleTextReader(held, submissions.Content), null)), size));
        var host = await SessionedHost.StartAsync(
            services => services
                .AddSingleton(staff).AddSingleton(new BatchRegister(held)).AddSingleton(new DomainListing(held)).AddSingleton(desk)
                .AddSingleton<DomainGrants>(new CuratorGrants(curator, SubmissionWorld.NameOf(SubmissionWorld.Domain))),
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                BatchRoutes.Map(routes);
            },
            Token);
        return new SampleReviewWorld(submissions, host, (outcome.Batch!.Value, curator, stranger), (size, messages, held));
    }

    internal string ReviewRoute => RouteOf(Batch);

    internal string DrawRoute => $"/lotes/{Batch}/amostra";

    internal static string RouteOf(long batch) => $"/lotes/{batch}/revisao";

    internal string MarkRoute(int place) => $"{ReviewRoute}/itens/{place}/revisado";

    internal string FindingRoute(int place) => $"{ReviewRoute}/itens/{place}/achado";

    internal string ApproveRoute => ApproveRouteOf(Batch);

    internal static string ApproveRouteOf(long batch) => $"{RouteOf(batch)}/aprovar";

    /// <summary>The page of an address as the person sees it, with whatever status the host answered.</summary>
    internal async Task<Reply> GetAsync(string route, long person)
    {
        using var response = await Host.GetAsync(route, Host.Sessions.Open(person), encrypted: true, Token);
        return new Reply(response.StatusCode, null, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)));
    }

    /// <summary>What a form posted to an address is answered with: the status, the place it sends the person to and the page when it shows one.</summary>
    internal async Task<Reply> PostAsync(string route, long person, params (string Name, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative))
        {
            Content = new FormUrlEncodedContent(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value))),
        };
        request.Headers.Accept.ParseAdd(Browser);
        using var response = await Host.SendAsync(request, Host.Sessions.Open(person), encrypted: true, Token);
        return new Reply(response.StatusCode, response.Headers.Location?.OriginalString, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)));
    }

    /// <summary>Draws the sample and marks the first <paramref name="reviewed" /> places of it as reviewed, as the curator would.</summary>
    internal Task DrawAndReviewAsync(int reviewed) => DrawAndReviewAsync(Batch, reviewed);

    /// <summary>The same for another batch of the domain, such as the copy of the first one.</summary>
    internal async Task DrawAndReviewAsync(long batch, int reviewed)
    {
        var drawn = await PostAsync($"/lotes/{batch}/amostra", Curator);
        Assert.Equal(HttpStatusCode.SeeOther, drawn.Status);
        for (var place = 1; place <= reviewed; place++)
        {
            Assert.Equal(HttpStatusCode.SeeOther, (await PostAsync($"{RouteOf(batch)}/itens/{place}/revisado", Curator)).Status);
        }
    }

    /// <summary>Submits the conversation of the world again: the same file, so the same digest, left open as a batch of its own.</summary>
    internal async Task<long> SubmitCopyAsync()
    {
        var outcome = await Submissions.SubmitAsync(SubmissionWorld.Request(SubmissionWorld.Messages(Entries(Messages))));
        Assert.Equal(SubmissionKind.Done, outcome.Kind);
        return outcome.Batch!.Value;
    }

    internal Task<BatchReview?> StateAsync() => StateAsync(Batch);

    internal Task<BatchReview?> StateAsync(long batch) => Reviews.ReadAsync(SubmissionWorld.Domain, batch, Token);

    /// <summary>The steps of the chain for an item or a batch, in order, as the owner reads them, one text each.</summary>
    /// <param name="kind">The kind of the item the events are about.</param>
    internal Task<IReadOnlyList<string>> StepsAsync(string kind) => Submissions.Database.FirstColumnAsOwnerAsync(
        $"SELECT coalesce(actor_person_id::text, '-') || ',' || step || ',' || reason FROM public.custody_event WHERE item_kind = '{kind}' ORDER BY sequence");

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await DeskSource.DisposeAsync();
        await Submissions.DisposeAsync();
    }

    private static (string, string)[] Entries(int messages) => [.. Enumerable.Range(0, messages).Select(at => ("ZQXREMETENTE", $"ZQXTEXTO{at}"))];

    private static async Task<long> PersonAsync(SubmissionWorld submissions, string name) =>
        long.Parse((await submissions.Database.FirstColumnAsOwnerAsync($"SELECT id::text FROM public.person WHERE name = '{name}'"))[0], CultureInfo.InvariantCulture);

    private sealed class CuratorGrants(long person, string domain) : DomainGrants
    {
        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult(entered == person ? new[] { domain }.AsEnumerable() : None);
    }
}

/// <summary>What the host answered: the status, the place it sends the person to when it does, and the page when it shows one.</summary>
/// <param name="status">The status of the answer.</param>
/// <param name="location">Where the answer sends the person to; none when it does not.</param>
/// <param name="page">The page, as the person reads it.</param>
internal sealed class Reply(HttpStatusCode status, string? location, string page)
{
    internal HttpStatusCode Status { get; } = status;

    internal string? Location { get; } = location;

    internal string Page { get; } = page;
}
