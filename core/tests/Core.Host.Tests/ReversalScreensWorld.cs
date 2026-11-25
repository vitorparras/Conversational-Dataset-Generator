using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Tests.Submissions;
using Core.Persistence.Content;
using Core.Persistence.Content.Writing;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests;

/// <summary>
/// The screens of the request to reverse a marker and of its authorization (issues 614 and 615) behind the real gates, the real routes and the desk the root registers, over a database of their
/// own: two people of the curation of the first domain, one of the curation of the second, and the person who annotates, granted the first. The key is read by the root, from a source of the
/// test that holds the labels of the map; the clock is the world's, so that a term can pass.
/// </summary>
internal sealed class ReversalScreensWorld : IAsyncDisposable
{
    /// <summary>The event the last act wrote: what it points to, the step, the reason, the reference, who acted and whether it was written just now.</summary>
    internal const string LastEvent =
        "SELECT item_kind || '|' || item_id || '|' || step || '|' || reason || '|' || coalesce(reference, '') || '|' || actor_person_id || '|' "
        + "|| (occurred_at > now() - interval '1 minute') FROM public.custody_event ORDER BY sequence DESC LIMIT 1";

    internal const string CountRequests = "SELECT count(*)::text FROM content.reversal_request";

    internal const string CountAuthorizations = "SELECT count(*)::text FROM content.reversal_authorization";

    internal const string CountEvents = "SELECT count(*)::text FROM public.custody_event";

    private static readonly string[] Labels = ["texto-bruto/1/v1", "texto-bruto/2/v1", "mapa-reversao/1/v1", "mapa-reversao/2/v1"];

    private readonly MapKeySource key;

    private ReversalScreensWorld(SubmissionWorld submissions, SessionedHost host, (long Curator, long Second, long Other) people, ManualClock clock, MapKeySource key)
    {
        Submissions = submissions;
        Host = host;
        (Curator, Second, Other) = people;
        Clock = clock;
        this.key = key;
    }

    internal SubmissionWorld Submissions { get; }

    internal SessionedHost Host { get; }

    internal ManualClock Clock { get; }

    /// <summary>The person of the curation of the first domain who asks.</summary>
    internal long Curator { get; }

    /// <summary>The other person of the curation of the first domain, who decides.</summary>
    internal long Second { get; }

    /// <summary>The person of the curation of the second domain alone.</summary>
    internal long Other { get; }

    internal static long Annotator => SubmissionWorld.Person;

    internal ReversalDesk Desk => Host.Services.GetRequiredService<ReversalDesk>();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [SuppressMessage("Reliability", "CA2000", Justification = "The world takes the submissions and the host it is given and lets go of them with itself.")]
    internal static async Task<ReversalScreensWorld> StartAsync()
    {
        var submissions = await SubmissionWorld.CreateAsync(labels: Labels);
        var database = submissions.Database;
        await database.RunAsOwnerAsync(
            [
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-curadora', 'Curadoria do acervo'), ('zqx-curadora-2', 'Curadoria do acervo'), ('zqx-curadora-b', 'Curadoria do acervo')",
                $"INSERT INTO public.derivation_label (label) VALUES {string.Join(", ", Labels.Select(label => $"('{label}')"))}",
                .. SeededBatches.Statements(1),
                "SELECT set_config('app.domain', '1', true)",
                ItemTexts.VersionRow(1),
            ]);
        var ids = await database.FirstColumnAsOwnerAsync("SELECT id::text FROM public.person WHERE name LIKE 'zqx-curadora%' ORDER BY name");
        (long Curator, long Second, long Other) people = (Parsed(ids[0]), Parsed(ids[1]), Parsed(ids[2]));
        var clock = new ManualClock();
        clock.Advance(DateTimeOffset.UtcNow - clock.GetUtcNow());
        var source = submissions.DataSource;
        var key = new MapKeySource();
        var host = await SessionedHost.StartAsync(
            services => services
                .AddSingleton(new ServiceCollection().AddSingleton(source).AddSingleton<IMasterKeySource>(key).BuildServiceProvider())
                .AddSingleton<TimeProvider>(clock)
                .AddSingleton(provider => new OperatorAdministration(
                    new PersonRegistry(source), new PersonCredentials(source, new PasswordDerivation(1)), provider.GetRequiredService<SessionStore>(), new PersonNotices(source)))
                .AddSingleton<DomainGrants>(new GrantedTo((SubmissionWorld.Domain, [people.Curator, people.Second, Annotator]), (SubmissionWorld.OtherDomain, [people.Other]))),
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                BatchRoutes.Map(routes);
            },
            Token);
        return new ReversalScreensWorld(submissions, host, people, clock, key);
    }

    /// <summary>A pseudonymized item of the domain with one marker of its own, written as the pseudonymization writes it.</summary>
    internal async Task<(long Item, string Marker)> ItemAsync()
    {
        const int domain = SubmissionWorld.Domain;
        var marker = ItemTexts.Marker();
        var item = ItemTexts.Of("zqx-bruto", "zqx-limpo", $"zqx {marker} zqx") with { Map = [new NewMapEntry(marker, "zqx-original-sintetico", false, OccurrenceDestination.Pseudonymize)] };
        var written = await new ContentWriter(Submissions.DataSource, Submissions.Content).AddPseudonymizedItemAsync(domain, domain, item, Token);
        return (Assert.NotNull(written.ItemId), marker);
    }

    internal static Dictionary<string, string> Draft(long item, string marker) => new(StringComparer.Ordinal)
    {
        ["item"] = item.ToString(CultureInfo.InvariantCulture),
        ["marcador"] = marker,
        ["prazo"] = "30",
        ["motivo"] = "zqx-motivo-sintetico",
        ["uso"] = "zqx-uso-sintetico",
    };

    /// <summary>A request sent by the screen by the person given, the first of the curation by default: its identifier, from the address the answer leads to, and its item.</summary>
    internal async Task<(long Id, long Item)> AskedAsync(long? person = null)
    {
        var (item, marker) = await ItemAsync();
        var sent = await PostAsync(ScreenPaths.ReversalAsk, Draft(item, marker), person);
        Assert.Equal(HttpStatusCode.SeeOther, sent.Status);
        var id = (sent.Location ?? throw new InvalidOperationException("The answer leads nowhere."))[(ScreenPaths.ReversalMine.Length + 1)..].Split('?')[0];
        return (Parsed(id), item);
    }

    /// <summary>The day after the one of the clock, as the field of the date takes it.</summary>
    internal string Tomorrow => DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime).AddDays(1).ToString("O", CultureInfo.InvariantCulture);

    /// <summary>The day after the one of the clock, as the screens show it.</summary>
    internal string TomorrowShown
    {
        get
        {
            var day = DateOnly.FromDateTime(Clock.GetUtcNow().UtcDateTime).AddDays(1);
            return string.Create(CultureInfo.InvariantCulture, $"{day.Day:00}/{day.Month:00}/{day.Year}");
        }
    }

    internal async Task<Reply> GetAsync(string route, long? person = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(route, UriKind.Relative));
        return await SendAsync(request, person);
    }

    internal async Task<Reply> PostAsync(string route, IReadOnlyDictionary<string, string> fields, long? person = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(route, UriKind.Relative)) { Content = new FormUrlEncodedContent(fields) };
        return await SendAsync(request, person);
    }

    /// <summary>
    /// The first value of the query, read as the superuser: the requests and their authorizations are held to the row policy of the domain even for the owner, and a count read so
    /// sees every row of every domain; an empty value reads as empty.
    /// </summary>
    [SuppressMessage("Security", "CA2100", Justification = "The queries are literals of the calling tests, never input.")]
    internal async Task<string> ScalarAsync(string query)
    {
        await using var connection = new NpgsqlConnection(Submissions.Database.SuperuserConnectionString);
        await connection.OpenAsync(Token);
        await using var command = new NpgsqlCommand(query, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(Token), CultureInfo.InvariantCulture) ?? "";
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await Submissions.DisposeAsync();
    }

    /// <summary>Makes the chain refuse, as broken, every event of the steps given from now on.</summary>
    internal Task RefuseInTheChainAsync(params string[] steps) =>
        Submissions.Database.ExecuteAsSuperuserAsync(
            "CREATE FUNCTION public.zqx_chain_refuses() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
            + "BEGIN RAISE EXCEPTION 'broken' USING ERRCODE = 'CE002'; END $f$; "
            + $"CREATE TRIGGER zqx_chain_refuses BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step IN ('{string.Join("', '", steps)}')) "
            + "EXECUTE FUNCTION public.zqx_chain_refuses()");

    /// <summary>
    /// Makes every authorization expired for the database as well, whose clock the world's does not move: the change is set aside for the one statement, as only the superuser can,
    /// so that a renewal is judged as the day after the term would.
    /// </summary>
    internal Task ExpireInTheDatabaseAsync() =>
        Submissions.Database.ExecuteAsSuperuserAsync(
            "ALTER TABLE content.reversal_authorization DISABLE TRIGGER reversal_authorization_never_changes; "
            + "UPDATE content.reversal_authorization SET authorized_at = now() - interval '2 days', valid_until = now() - interval '1 day'; "
            + "ALTER TABLE content.reversal_authorization ENABLE ALWAYS TRIGGER reversal_authorization_never_changes");

    /// <summary>From now on the key reads as unreadable, as a file damaged after the start would.</summary>
    internal void LoseTheKey() => key.Lost = true;

    private static long Parsed(string id) => long.Parse(id, NumberStyles.None, CultureInfo.InvariantCulture);

    private async Task<Reply> SendAsync(HttpRequestMessage request, long? person)
    {
        request.Headers.Accept.ParseAdd(SampleReviewWorld.Browser);
        using var response = await Host.SendAsync(request, Host.Sessions.Open(person ?? Curator), encrypted: true, Token);
        return new Reply(response.StatusCode, response.Headers.Location?.OriginalString, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)));
    }

    /// <summary>The key of the test, with the labels of the raw text and of the map of both domains, as the installation would load them.</summary>
    private sealed class MapKeySource : IMasterKeySource
    {
        public string Origin => "zqx-test";

        internal bool Lost { get; set; }

        public Task<MasterKeyReading> ReadAsync(CancellationToken cancellationToken)
        {
            if (Lost)
            {
                return Task.FromResult(MasterKeyReading.Unreadable("zqx-chave-ilegivel"));
            }

            var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
            var secret = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + 11))).ToArray();
            return Task.FromResult(MasterKeyReading.FromBytes(key, DerivationLabels.Of(Labels), CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = secret }), "zqx-test"));
        }
    }

    private sealed class GrantedTo(params (int Domain, long[] People)[] grants) : DomainGrants
    {
        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<string>>([.. grants.Where(grant => grant.People.Contains(entered)).Select(grant => SubmissionWorld.NameOf(grant.Domain))]);
    }
}
