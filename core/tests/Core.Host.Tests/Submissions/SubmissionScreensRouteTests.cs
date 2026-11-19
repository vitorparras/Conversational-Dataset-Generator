using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Core.Boundary.Contracts;
using Core.Host.Endpoints;
using Core.Host.Operators;
using Core.Host.Screens;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Sessions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The screen of the submission and the detail of a batch over the host (#607, #609): the form posts the file, the service judges it, and the answer is the open batch, the verdict that says
/// why it was refused, the field marked, or the gate that says why it is closed. Nothing of the material, of the origin as written or of the name of the file comes back in a page.
/// </summary>
[Trait("Category", "Database")]
public sealed class SubmissionScreensRouteTests
{
    private const string Path = "zqx-pasta/conversa.json";

    private static readonly string[] EmptyReasons = ["format_not_confirmed", "unreadable", "no_units"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<long, string, string> RefusedByTheGate => new()
    {
        { 16, SubmissionWorld.Format, ReasonCodes.TooLarge },
        { 1_000_000, "zqx-formato/9", "unknown_format" },
    };

    [DatabaseFact]
    public async Task SubmissionWithoutOriginReturnsTheErrorOnTheFieldAndWritesNothing()
    {
        await using var world = await World.StartAsync();

        var (status, _, page) = await world.PostAsync(Fields(origin: null));

        Assert.Equal((HttpStatusCode.BadRequest, 0L), (status, await world.Inner.BatchCountAsync()));
        Assert.Contains("origem-erro", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SubmissionWithoutConditionReturnsTheErrorOnTheFieldAndWritesNothing()
    {
        await using var world = await World.StartAsync();

        var (status, _, page) = await world.PostAsync(Fields(condition: null));

        Assert.Equal((HttpStatusCode.BadRequest, 0L), (status, await world.Inner.BatchCountAsync()));
        Assert.Contains(SubmissionTexts.FieldErrorOf("condition", "missing"), page, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [MemberData(nameof(RefusedByTheGate))]
    public async Task FileRefusedByTheGateShowsItsCode(long maxBytes, string format, string code)
    {
        await using var world = await World.StartAsync(maxBytes);

        var (status, _, page) = await world.PostAsync(Fields(format: format));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(SubmissionTexts.ReasonOf(code), page, StringComparison.Ordinal);
    }

    [DatabaseTheory]
    [MemberData(nameof(RefusedByTheGate))]
    public async Task FileRefusedByTheGateIsNotEchoed(long maxBytes, string format, string code)
    {
        await using var world = await World.StartAsync(maxBytes);

        var (_, _, page) = await world.PostAsync(Fields(format: format));

        Assert.False(page.Contains("ZQXPRIMEIRO", StringComparison.Ordinal) || page.Contains(SubmissionWorld.Origin, StringComparison.Ordinal), code);
    }

    [DatabaseTheory]
    [MemberData(nameof(RefusedByTheGate))]
    public async Task FileRefusedByTheGateIsRecordedAsARejectedBatch(long maxBytes, string format, string code)
    {
        await using var world = await World.StartAsync(maxBytes);

        _ = await world.PostAsync(Fields(format: format));

        var batch = Assert.Single(await world.BatchesAsync());
        Assert.Equal((BatchState.Rejected, code), (batch.State, batch.Reason));
    }

    [DatabaseFact]
    public async Task PathNameIsNeverEchoed()
    {
        await using var world = await World.StartAsync();

        var (_, location, _) = await world.PostAsync(Fields(), name: Path);
        var detail = await world.PageAsync(location!);

        Assert.DoesNotContain("zqx-pasta", detail, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PathNameIsRecordedOnlyByItsCode()
    {
        await using var world = await World.StartAsync();

        _ = await world.PostAsync(Fields(), name: Path);

        var batch = Assert.Single(await world.BatchesAsync());
        Assert.Equal("name_with_path", batch.Receipt!.Warning);
    }

    [DatabaseFact]
    public async Task PathNameShowsTheWarningOnTheBatch()
    {
        await using var world = await World.StartAsync();
        var (_, location, _) = await world.PostAsync(Fields(), name: Path);

        var detail = await world.PageAsync(location!);

        Assert.Contains(BatchTexts.WarningOf("name_with_path"), detail, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SubmissionWithAPathNameIsAccepted()
    {
        await using var world = await World.StartAsync();

        var (status, location, _) = await world.PostAsync(Fields(), name: Path);

        var batch = Assert.Single(await world.BatchesAsync());
        Assert.Equal((HttpStatusCode.SeeOther, ScreenPaths.BatchOf(batch.Id), BatchState.Open), (status, location, batch.State));
    }

    [DatabaseFact]
    public async Task EmptyFileIsRefusedAndLeavesNoOpenBatch()
    {
        await using var world = await World.StartAsync();

        var (status, _, page) = await world.PostAsync(Fields(), content: string.Empty);

        var batch = Assert.Single(await world.BatchesAsync());
        Assert.Equal((HttpStatusCode.OK, BatchState.Rejected), (status, batch.State));
        Assert.Contains(batch.Reason, EmptyReasons);
        Assert.Contains(SubmissionTexts.ReasonOf(batch.Reason!), page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ClosedGateSaysWhyItIsClosed()
    {
        await using var world = await World.StartAsync(gateOpen: false);

        using var response = await world.Host.GetAsync(ScreenPaths.BatchNew, world.Host.Sessions.Open(SubmissionWorld.Person), encrypted: true, Token);
        var page = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(SubmissionTexts.BlockedOf("recovery_not_rehearsed"), page, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"file\"", page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ClosedGateOffersNoUpload()
    {
        await using var world = await World.StartAsync(gateOpen: false);

        var (status, _, page) = await world.PostAsync(Fields());

        Assert.Equal((HttpStatusCode.ServiceUnavailable, 0L), (status, await world.Inner.BatchCountAsync()));
        Assert.True(page.Contains("envio-fechado", StringComparison.Ordinal) && !page.Contains("type=\"file\"", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task AcceptedSubmissionLeadsToTheOpenBatch()
    {
        await using var world = await World.StartAsync();
        var (_, location, _) = await world.PostAsync(Fields());

        var detail = await world.PageAsync(location!);

        var batch = Assert.Single(await world.BatchesAsync());
        Assert.Equal(ScreenPaths.BatchOf(batch.Id), location);
        Assert.Contains(BatchTexts.StateOf(BatchState.Open), detail, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BatchDetailCountsByFormDestinationAndReplica()
    {
        await using var world = await World.StartAsync(replication: new ReplicationRule(minLength: 10, minSenders: 2));
        const string Forwarded = "ZQXENCAMINHADOLONGO";
        var (_, location, _) = await world.PostAsync(Fields(), content: SubmissionWorld.Messages(("ZQXUM", Forwarded), ("ZQXDOIS", Forwarded)));

        var detail = await world.PageAsync(location!);

        Assert.Matches(@"<dt>Conversa</dt>\s*<dd>2 itens</dd>", detail);
        Assert.Contains(BatchDetailTexts.NotCounted, detail, StringComparison.Ordinal);
        Assert.DoesNotContain("<dt>Removidos</dt>", detail, StringComparison.Ordinal);
        Assert.Matches(@"<dt>Mensagens marcadas como réplica</dt>\s*<dd>2</dd>", detail);
    }

    [DatabaseFact]
    public async Task ResubmissionShowsTheBatchThatRecognizedIt()
    {
        await using var world = await World.StartAsync();
        var (_, location, _) = await world.PostAsync(Fields());
        var first = Assert.Single(await world.BatchesAsync()).Id;
        await world.Inner.AcceptReviewedAsync(first);
        _ = await world.PostAsync(Fields(), name: "outro-nome.json");
        var again = (await world.BatchesAsync()).Single(batch => batch.Id != first).Id;

        var detail = await world.PageAsync(ScreenPaths.BatchOf(again));

        Assert.Contains($"href=\"{location}\"", detail, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task AccommodatedFormShowsTheDeclaredLoss()
    {
        await using var world = await World.StartAsync();
        var batch = await world.AccommodatedAsync();

        var detail = await world.PageAsync(ScreenPaths.BatchOf(batch));

        Assert.Contains("lote-perda", detail, StringComparison.Ordinal);
        Assert.Contains(BatchDetailTexts.LossOf("author"), detail, StringComparison.Ordinal);
        Assert.Contains($"veio como {BatchDetailTexts.FormOf("conversation")} e foi acomodado como {BatchDetailTexts.FormOf("document")}", detail, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BatchDetailNeverShowsMaterialText()
    {
        await using var world = await World.StartAsync();
        var (_, location, _) = await world.PostAsync(Fields());

        var detail = await world.PageAsync(location!);

        Assert.False(
            detail.Contains("ZQXPRIMEIRO", StringComparison.Ordinal) || detail.Contains("ZQXUM", StringComparison.Ordinal)
            || detail.Contains(SubmissionWorld.Origin, StringComparison.Ordinal) || detail.Contains("conversa.json", StringComparison.Ordinal));
    }

    [DatabaseFact]
    public async Task SameFileAsAnAcceptedBatchIsRejectedAsAlreadyReceived()
    {
        await using var world = await World.StartAsync();
        _ = await world.PostAsync(Fields());
        await world.Inner.AcceptReviewedAsync(Assert.Single(await world.BatchesAsync()).Id);

        var (status, _, page) = await world.PostAsync(Fields());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains(SubmissionTexts.ReasonOf(ReasonCodes.AlreadyReceived), page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task DatabaseLostWhileWritingAnswersThatNothingWasKept()
    {
        await using var world = await World.StartAsync(lostWhileWriting: true);

        var (status, _, page) = await world.PostAsync(Fields());

        Assert.Equal((HttpStatusCode.ServiceUnavailable, 0L), (status, await world.Inner.ItemRowsAsync()));
        Assert.Contains(SubmissionTexts.NotKept, page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BatchOfAnotherDomainAnswersNotFound()
    {
        await using var world = await World.StartAsync();
        var other = await world.Inner.Batches.OpenAsync(new BatchOpening(SubmissionWorld.OtherDomain, SubmissionWorld.Person), Token);

        using var response = await world.Host.GetAsync(ScreenPaths.BatchOf(other.BatchId!.Value), world.Host.Sessions.Open(SubmissionWorld.Person), encrypted: true, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(NotFoundScreen.Title, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task BatchZeroAnswersNotFound()
    {
        await using var world = await World.StartAsync();

        using var response = await world.Host.GetAsync(ScreenPaths.BatchOf(0), world.Host.Sessions.Open(SubmissionWorld.Person), encrypted: true, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(NotFoundScreen.Title, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task FileNotChosenReturnsTheErrorOnTheFileAndWritesNothing()
    {
        await using var world = await World.StartAsync();

        var (status, _, page) = await world.PostAsync(Fields(), content: string.Empty, name: string.Empty);

        Assert.Equal((HttpStatusCode.BadRequest, 0L), (status, await world.Inner.BatchCountAsync()));
        Assert.Contains(SubmissionTexts.FieldErrorOf(SubmissionTexts.FileField, "missing"), page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task FileFarPastTheLimitIsRecordedAsTooLargeAndAnsweredByTheScreen()
    {
        await using var world = await World.StartAsync(maxBytes: 16);

        var (status, _, page) = await world.PostAsync(Fields(), content: new string('z', 300_000));

        var batch = Assert.Single(await world.BatchesAsync());
        Assert.Equal((HttpStatusCode.OK, BatchState.Rejected, ReasonCodes.TooLarge), (status, batch.State, batch.Reason));
        Assert.Contains(SubmissionTexts.ReasonOf(ReasonCodes.TooLarge), page, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task SubmissionWithoutCurationIsRefusedAndWritesNothing()
    {
        await using var world = await World.StartAsync();

        var (status, _, _) = await world.PostAsync(Fields(), person: World.Annotator);

        Assert.Equal((HttpStatusCode.NotFound, 0L), (status, await world.Inner.BatchCountAsync()));
    }

    internal static Dictionary<string, string?> Fields(string? origin = SubmissionWorld.Origin, string? condition = SubmissionWorld.Loosest, string format = SubmissionWorld.Format) =>
        new(StringComparer.Ordinal)
        {
            ["domain"] = "1",
            ["declaredForm"] = "conversation",
            ["sourceFormat"] = format,
            ["condition"] = condition,
            ["origin"] = origin,
        };

    /// <summary>The host with the screens of the batches over the world of the submissions, where person 1 curates the domain granted and person 2 annotates.</summary>
    internal sealed class World : IAsyncDisposable
    {
        internal const long Annotator = 2;

        private readonly NpgsqlDataSource lost;

        private World(SubmissionWorld inner, SessionedHost host, NpgsqlDataSource lost)
        {
            Inner = inner;
            Host = host;
            this.lost = lost;
        }

        internal SubmissionWorld Inner { get; }

        internal SessionedHost Host { get; }

        internal static async Task<World> StartAsync(
            long maxBytes = 1_000_000, bool gateOpen = true, ReplicationRule? replication = null, bool lostWhileWriting = false, IRecognizer? recognizer = null)
        {
            var inner = await SubmissionWorld.CreateAsync(maxBytes, gateOpen, replication: replication, recognizer: recognizer);
            await inner.Database.RunAsOwnerAsync(
                "UPDATE public.person SET function_name = 'Curadoria do acervo' WHERE id = 1",
                "INSERT INTO public.person (name, function_name) VALUES ('zqx-anotadora', 'Anotação')");
            var lost = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Database=zqx;Username=zqx;Password=zqx;Timeout=2");
            var service = lostWhileWriting ? LostService(inner, lost, maxBytes) : inner.Service;
            var source = inner.DataSource;
            var staff = new OperatorAdministration(
                new PersonRegistry(source),
                new PersonCredentials(source, new PasswordDerivation(1)),
                new SessionStore(new SessionLifetimes(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8)), TimeProvider.System),
                new PersonNotices(source));
            var host = await SessionedHost.StartAsync(
                services => services.AddSingleton(staff).AddSingleton(SubmissionServiceHolder.Of(service, maxBytes)).AddSingleton(new BatchRegister(source))
                    .AddSingleton(new IntakeDoor(source)).AddSingleton(new DomainListing(source)).AddSingleton(new BatchCounts(source))
                    .AddSingleton<DomainGrants>(new GrantsByName(SubmissionWorld.NameOf(SubmissionWorld.Domain))),
                routes =>
                {
                    ScreenGate.Use((IApplicationBuilder)routes);
                    BatchScreensEndpoint.Map(routes);
                },
                Token);
            return new World(inner, host, lost);
        }

        [SuppressMessage("Reliability", "CA2000", Justification = "The message and its content are disposed with the response, by the host that sends them.")]
        internal async Task<(HttpStatusCode Status, string? Location, string Page)> PostAsync(
            Dictionary<string, string?> fields, string content = SubmissionWorld.Conversation, string name = "conversa.json", long person = SubmissionWorld.Person)
        {
            var form = new MultipartFormDataContent();
            foreach (var field in fields.Where(field => field.Value is not null))
            {
                form.Add(new StringContent(field.Value!), field.Key);
            }

            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            if (name.Length == 0)
            {
                file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "\"file\"", FileName = "\"\"" };
                form.Add(file);
            }
            else
            {
                form.Add(file, "file", name);
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ScreenPaths.BatchNew, UriKind.Relative)) { Content = form };
            using var response = await Host.SendAsync(request, Host.Sessions.Open(person), encrypted: true, Token);
            return (response.StatusCode, response.Headers.Location?.OriginalString, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token)));
        }

        internal async Task<string> PageAsync(string route)
        {
            using var response = await Host.GetAsync(route, Host.Sessions.Open(SubmissionWorld.Person), encrypted: true, Token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(Token));
        }

        /// <summary>The batches of the domain granted, as the register of the submissions reads them.</summary>
        internal Task<IReadOnlyList<BatchEntry>> BatchesAsync() =>
            Inner.Service.ListAsync(new BatchFilter { Domains = [SubmissionWorld.Domain] }, Token);

        /// <summary>A batch declared as a document whose two units, read from a conversation, were stored with the author and the instant they lose.</summary>
        internal async Task<long> AccommodatedAsync()
        {
            var conditions = OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]);
            var opened = await Inner.Batches.OpenAsync(new BatchOpening(SubmissionWorld.Domain, SubmissionWorld.Person) { DeclaredForm = "document", SourceForm = "conversation" }, Token);
            var instant = new DateTimeOffset(2031, 3, 4, 8, 0, 0, TimeSpan.Zero);
            var material = new ReadMaterial([new ReadUnit("ZQXTERCEIRO", "ZQXUM", instant, null, []), new ReadUnit("ZQXQUARTO", "ZQXDOIS", instant, null, [])]);
            var kept = new SubmissionStoring.Kept(SubmissionWorld.Domain, opened.BatchId!.Value, material, MaterialForm.Document, SubmissionWorld.AccommodatedAsDocument(material));
            var intake = SubmissionWorld.Intake(1_000_000, conditions, Inner.Content, Inner.DataSource);
            Assert.Equal(SubmissionStoring.Keeping.Kept, await new SubmissionStoring.Keeper(Inner.Batches, intake, NullLogger.Instance).KeepAsync(kept, Token));
            return opened.BatchId.Value;
        }

        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync();
            await lost.DisposeAsync();
            await Inner.DisposeAsync();
        }

        /// <summary>A service over the world whose writer of the items reaches a database that is gone, so that the batch opens and its items cannot be written.</summary>
        private static SubmissionService LostService(SubmissionWorld inner, NpgsqlDataSource lost, long maxBytes)
        {
            var conditions = OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]);
            var intake = SubmissionWorld.Intake(maxBytes, conditions, inner.Content, lost);
            return new SubmissionService(intake, inner.Batches, inner.DataSource, NullLogger.Instance);
        }
    }
}
