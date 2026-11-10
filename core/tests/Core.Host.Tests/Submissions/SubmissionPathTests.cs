using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The path of a file from the door to the items (issue 79, part 1): what was read and conferred is kept as one item per unit with its raw text sealed and the batch accepted;
/// what was turned back leaves the batch rejected with the reason and no item; and when the key that seals is not there, or the writing fails, the path says so by a code
/// and keeps nothing of the material, registering the submission as rejected with the motive and no item (CA-4).
/// </summary>
public sealed class SubmissionPathTests
{
    private const string Declared = "zqx_sigilosa,zqx_media,zqx_livre";

    private static readonly Dictionary<string, string> Values = new()
    {
        ["Ingestion:Conditions"] = Declared,
        ["Ingestion:MaxBytes"] = "1000000",
    };

    [DatabaseFact]
    public async Task FileConferredIsKeptAsOneItemPerUnitAndTheBatchIsAccepted()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionState.Accepted, BatchState.Accepted, 2L), (outcome.State, entry!.State, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task FormThatTheMaterialDoesNotHaveLeavesTheBatchRejectedWithTheReasonAndNoItem()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var request = SubmissionWorld.Request() with { DeclaredForm = MaterialForm.Comment };

        var outcome = await world.SubmitAsync(request);
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionState.Rejected, BatchState.Rejected, outcome.Reason!.Code, 0L), (outcome.State, entry!.State, entry.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task FileThatTheReaderCannotReadLeavesTheBatchRejectedWithTheReasonAndNoItem()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(content: "isto nao e um arquivo de conversa"));
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal((SubmissionState.Rejected, BatchState.Rejected, outcome.Reason!.Code, 0L), (outcome.State, entry!.State, entry.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DomainThatHasNoKeyToSealWithKeepsNothingNotTheBatchNorTheItemWhateverTheOrigin(bool originUnknown)
    {
        await using var world = await SubmissionWorld.CreateAsync(labels: ["texto-bruto/2/v1"]);
        var request = SubmissionWorld.Request() with { OriginUnknown = originUnknown, Origin = originUnknown ? null : SubmissionWorld.Origin };

        var outcome = await world.SubmitAsync(request);

        Assert.Equal((SubmissionKind.NotRecorded, BatchOutcome.DomainUnknown, 0L, 0L), (outcome.Kind, outcome.Failure, await world.BatchCountAsync(), await world.ItemRowsAsync()));
    }

    [DatabaseFact]
    public async Task WritingThatFailsAfterTheBatchOpenedRejectsItWithNoItemAndSaysSoByACode()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var conditions = OriginConditions.Of([SubmissionWorld.Strictest]);
        await using var broken = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Username=u;Password=p;Database=d;Timeout=2");
        var intake = SubmissionWorld.Intake(1_000_000, conditions, world.Content, broken);
        var service = new SubmissionService(intake, new IngestionBatches(world.DataSource, conditions), new BatchRegister(world.DataSource), world.DataSource);

        var outcome = await service.SubmitAsync(SubmissionWorld.Request(), TestContext.Current.CancellationToken);
        var entry = await service.DetailAsync([SubmissionWorld.Domain], outcome.Batch!.Value, TestContext.Current.CancellationToken);

        Assert.Equal(
            (SubmissionKind.Unavailable, "write_failed", BatchState.Rejected, "write_failed", 0L),
            (outcome.Kind, outcome.Code, entry!.State, entry.Reason, await world.ItemRowsAsync()));
    }

    [DatabaseTheory]
    [InlineData(MasterKeyFailureKind.Absent, "absent")]
    [InlineData(MasterKeyFailureKind.Unreadable, "unreadable")]
    [InlineData(MasterKeyFailureKind.WrongSize, "wrong_size")]
    public async Task KeyThatSealsTheContentNotThereMakesTheRouteSay503AndRegistersTheSubmissionRejectedWithNoItemAndNoMaterial(MasterKeyFailureKind kind, string told)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var startup = new ServiceCollection().AddSingleton(world.DataSource).AddSingleton<IMasterKeySource>(new FailingKeySource(kind)).BuildServiceProvider();
        await using var host = await NoKeyHostAsync(startup, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(SubmissionWorld.Conversation);

        using var response = await host.SendAsync(Form(), host.SeedSession(), encrypted: true, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(body);
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], json.RootElement.GetProperty("batch").GetInt64(), cancellationToken);

        Assert.Equal(
            (HttpStatusCode.ServiceUnavailable, "content_key_unavailable", told),
            (response.StatusCode, json.RootElement.GetProperty("code").GetString(), json.RootElement.GetProperty("reason").GetString()));
        Assert.Equal(
            (BatchState.Rejected, "content_key_unavailable", SubmissionWorld.Person, bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes))),
            (entry!.State, entry.Reason, entry.SubmittedBy, entry.Receipt!.Bytes, entry.Receipt.Digest));
        Assert.Equal((1L, 0L, 0L), (await world.BatchCountAsync(), await world.ItemRowsAsync(), await world.OriginRowsAsync()));
        Assert.DoesNotContain("ZQXPRIMEIRO", body, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OriginDeclaredWithNoKeyToSealItStaysDeclaredAndItsTextIsKeptNowhere()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var startup = new ServiceCollection().AddSingleton(world.DataSource).AddSingleton<IMasterKeySource>(new FailingKeySource(MasterKeyFailureKind.Absent)).BuildServiceProvider();
        await using var host = await NoKeyHostAsync(startup, cancellationToken);

        using var response = await host.SendAsync(Form(originUnknown: false), host.SeedSession(), encrypted: true, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(body);
        var entry = await world.Service.DetailAsync([SubmissionWorld.Domain], json.RootElement.GetProperty("batch").GetInt64(), cancellationToken);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, "declared", 0L), (response.StatusCode, entry!.OriginState, await world.OriginRowsAsync()));
        Assert.DoesNotContain(SubmissionWorld.Origin, await world.Database.DumpAsync(), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task HolderReadsTheKeyAgainAtEveryUseAndKeepsNeitherTheKeyNorTheFailure()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var attempts = 0;
        var holder = new SubmissionServiceHolder(1, _ =>
        {
            attempts++;
            return Task.FromResult<SubmissionServiceHolder.Loaded?>(new SubmissionServiceHolder.Loaded(world.Service, null, MasterKeyFailureKind.Absent));
        });

        var first = await holder.GetAsync(TestContext.Current.CancellationToken);
        var second = await holder.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal((2, MasterKeyFailureKind.Absent, MasterKeyFailureKind.Absent), (attempts, first!.Unsealed, second!.Unsealed));
    }

    private static Task<SessionedHost> NoKeyHostAsync(ServiceProvider startup, CancellationToken cancellationToken) =>
        SessionedHost.StartAsync(
            services => services.AddSingleton(startup)
                .AddSingleton(provider => SubmissionParts.BatchesOf(provider, Values))
                .AddSingleton(provider => SubmissionParts.ServicesOf(provider, Values))
                .AddSingleton<DomainGrants>(new GrantsByName(SubmissionWorld.NameOf(SubmissionWorld.Domain)))
                .AddSingleton(new DomainListing(startup.GetRequiredService<NpgsqlDataSource>())),
            SubmissionsEndpoint.Map,
            cancellationToken);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The contents belong to the multipart form, which the request disposes.")]
    private static HttpRequestMessage Form(bool originUnknown = true)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("1"), "domain" },
            { new StringContent("conversation"), "declaredForm" },
            { new StringContent(SubmissionWorld.Format), "sourceFormat" },
            { new StringContent(originUnknown ? "true" : SubmissionWorld.Origin), originUnknown ? "originUnknown" : "origin" },
            { new ByteArrayContent(Encoding.UTF8.GetBytes(SubmissionWorld.Conversation)), "file", "conversa.json" },
        };
        return new HttpRequestMessage(HttpMethod.Post, new Uri(SubmissionsEndpoint.SubmissionsRoute, UriKind.Relative)) { Content = form };
    }
}
