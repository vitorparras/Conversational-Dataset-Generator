using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Core.Host.Endpoints;
using Core.Host.Submissions;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The routes of the ingestion in process, behind the session gate, over a migrated database (#75, part 2c): 201 with the receipt and no word of the material, 400 with the fields at fault and
/// nothing recorded, 413 registered as rejected, 401 with no session, a domain not granted or a function that does not hold the route as a route that does not exist,
/// and the origin only as declared or unknown.
/// </summary>
public sealed class SubmissionRoutesTests
{
    private const string Batches = SubmissionsEndpoint.BatchesRoute;
    private const string Submit = SubmissionsEndpoint.SubmissionsRoute;

    [DatabaseFact]
    public async Task FileAcceptedIsAnswered201WithTheReceiptAndNoWordOfTheMaterialOfTheOriginOrOfTheNameOfTheFile()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        using var response = await host.SendAsync(Form(Fields(SubmissionWorld.Origin)), host.SeedSession(), encrypted: true, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var json = JsonDocument.Parse(body);

        Assert.Equal(
            (HttpStatusCode.Created, "accepted", "declared", JsonValueKind.Null, 1L),
            (
                response.StatusCode,
                json.RootElement.GetProperty("state").GetString(),
                json.RootElement.GetProperty("origin").GetString(),
                json.RootElement.GetProperty("reason").ValueKind,
                await world.BatchCountAsync()));
        Assert.DoesNotContain("ZQXPRIMEIRO", body, StringComparison.Ordinal);
        Assert.DoesNotContain(SubmissionWorld.Origin, body, StringComparison.Ordinal);
        Assert.DoesNotContain("conversa.json", body, StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task OriginDeclaredUnknownIsAnsweredAsUnknownAndSealsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        using var response = await host.SendAsync(Form(Fields(origin: null, originUnknown: true)), host.SeedSession(), encrypted: true, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal((HttpStatusCode.Created, "unknown", 0L), (response.StatusCode, json.RootElement.GetProperty("origin").GetString(), await world.OriginRowsAsync()));
    }

    [DatabaseFact]
    public async Task BodyWithNoFileAndFieldsMissingIsAnswered400NamingEachFieldAndRecordsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Submit, UriKind.Relative)) { Content = OnlyTheForm() };
        using var response = await host.SendAsync(request, host.SeedSession(), encrypted: true, cancellationToken);
        var fields = await FieldsOfAsync(response, cancellationToken);

        Assert.Equal((HttpStatusCode.BadRequest, 0L), (response.StatusCode, await world.BatchCountAsync()));
        Assert.Contains("domain:missing", fields);
        Assert.Contains("sourceFormat:missing", fields);
        Assert.Contains("file:missing", fields);
    }

    [DatabaseFact]
    public async Task FileOverTheLimitIsAnswered413WithTheBodyOfARejectionAndIsRegistered()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync(maxBytes: 10);
        await using var host = await StartAsync(world, cancellationToken, maxBytes: 10);

        using var response = await host.SendAsync(Form(Fields(SubmissionWorld.Origin)), host.SeedSession(), encrypted: true, cancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(
            (HttpStatusCode.RequestEntityTooLarge, "rejected", "too_large", 1L),
            (response.StatusCode, json.RootElement.GetProperty("state").GetString(), json.RootElement.GetProperty("reason").GetProperty("code").GetString(), await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task GateClosedAnswers409WithTheCodeAndTheGateRouteSaysBlocked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync(gateOpen: false);
        await using var host = await StartAsync(world, cancellationToken);
        var identifier = host.SeedSession();

        using var submitted = await host.SendAsync(Form(Fields(SubmissionWorld.Origin)), identifier, encrypted: true, cancellationToken);
        using var gate = await host.GetAsync(SubmissionsEndpoint.GateRoute, identifier, encrypted: true, cancellationToken);
        using var json = JsonDocument.Parse(await gate.Content.ReadAsStringAsync(cancellationToken));

        Assert.Equal(
            (HttpStatusCode.Conflict, "blocked", "recovery_not_rehearsed", 0L),
            (submitted.StatusCode, json.RootElement.GetProperty("state").GetString(), json.RootElement.GetProperty("code").GetString(), await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task WithNoSessionEveryRouteIsAnswered401AndNothingIsRecorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        using var submitted = await host.SendAsync(Form(Fields(SubmissionWorld.Origin)), identifier: null, encrypted: true, cancellationToken);
        using var listed = await host.GetAsync(Batches, identifier: null, encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, 0L), (submitted.StatusCode, listed.StatusCode, await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task DomainNotGrantedIsADomainThatDoesNotExistAndNothingIsRecorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken, granted: SubmissionWorld.OtherDomain);

        using var response = await host.SendAsync(Form(Fields(SubmissionWorld.Origin)), host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.NotFound, 0L), (response.StatusCode, await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task ListAndDetailShowOnlyTheBatchesOfTheDomainsGrantedAndNeverTheOrigin()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var granted = await StartAsync(world, cancellationToken);
        var identifier = granted.SeedSession();
        using var created = await granted.SendAsync(Form(Fields(SubmissionWorld.Origin)), identifier, encrypted: true, cancellationToken);
        await using var other = await StartAsync(world, cancellationToken, granted: SubmissionWorld.OtherDomain);
        var otherIdentifier = other.SeedSession();

        using var accepted = await granted.GetAsync($"{Batches}?state=accepted", identifier, encrypted: true, cancellationToken);
        using var rejected = await granted.GetAsync($"{Batches}?state=rejected", identifier, encrypted: true, cancellationToken);
        using var hidden = await other.GetAsync(Batches, otherIdentifier, encrypted: true, cancellationToken);
        using var detail = await granted.GetAsync($"{Batches}/1", identifier, encrypted: true, cancellationToken);
        using var hiddenDetail = await other.GetAsync($"{Batches}/1", otherIdentifier, encrypted: true, cancellationToken);

        Assert.Equal(
            (HttpStatusCode.Created, 1, 0, 0, HttpStatusCode.OK, HttpStatusCode.NotFound),
            (
                created.StatusCode,
                await CountAsync(accepted, cancellationToken),
                await CountAsync(rejected, cancellationToken),
                await CountAsync(hidden, cancellationToken),
                detail.StatusCode,
                hiddenDetail.StatusCode));
        Assert.DoesNotContain(SubmissionWorld.Origin, await detail.Content.ReadAsStringAsync(cancellationToken), StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task ParameterOfTheListThatIsNotAcceptedIsAnswered400NamingTheField()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);

        using var response = await host.GetAsync($"{Batches}?state=zqx&from=ontem&page=0", host.SeedSession(), encrypted: true, cancellationToken);
        var fields = await FieldsOfAsync(response, cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(["state:invalid", "from:invalid", "page:invalid"], fields);
    }

    [DatabaseTheory]
    [InlineData("GET", SubmissionsEndpoint.GateRoute)]
    [InlineData("GET", Batches)]
    [InlineData("POST", Submit)]
    public async Task WhoseFunctionDoesNotHoldTheRouteGetsTheAnswerOfARouteThatDoesNotExist(string method, string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await SessionedHost.StartAsync(
            services => services.AddSingleton(SubmissionServiceHolder.Of(world.Service, 1_000_000)).AddSingleton<IGrantedDomains>(new GrantedTo(SubmissionWorld.Domain)),
            routes =>
            {
                ScreenGate.Use((IApplicationBuilder)routes);
                SubmissionsEndpoint.Map(routes);
            },
            cancellationToken);

        using var response = await host.SendAsync(new HttpRequestMessage(new HttpMethod(method), new Uri(route, UriKind.Relative)), host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.NotFound, 0L, 0L), (response.StatusCode, (await response.Content.ReadAsByteArrayAsync(cancellationToken)).LongLength, await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task KeyThatCannotBeReadIsA503OnTheSubmissionAndNothingIsRecorded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken, holder: NoKey());

        using var response = await host.SendAsync(Form(Fields(SubmissionWorld.Origin)), host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.ServiceUnavailable, 0L), (response.StatusCode, await world.BatchCountAsync()));
    }

    [DatabaseFact]
    public async Task GateListAndDetailAnswerWithNoKeyAtAllBecauseNoneOfThemReadsOne()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        var accepted = await world.SubmitAsync(SubmissionWorld.Request());
        await using var host = await StartAsync(world, cancellationToken, holder: NoKey());
        var identifier = host.SeedSession();

        using var gate = await host.GetAsync(SubmissionsEndpoint.GateRoute, identifier, encrypted: true, cancellationToken);
        using var listed = await host.GetAsync(Batches, identifier, encrypted: true, cancellationToken);
        using var detail = await host.GetAsync($"{Batches}/{accepted.Batch}", identifier, encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (gate.StatusCode, listed.StatusCode, detail.StatusCode));
        Assert.Equal(1, await CountAsync(listed, cancellationToken));
    }

    [DatabaseFact]
    public async Task BodyWithMoreFieldsThanTheFormHasBeforeTheFileIsNotReadPastThemAndRecordsNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var world = await SubmissionWorld.CreateAsync();
        await using var host = await StartAsync(world, cancellationToken);
        var fields = Fields(SubmissionWorld.Origin);
        for (var extra = 0; extra < 40; extra++)
        {
            fields[$"zqx{extra}"] = "x";
        }

        using var response = await host.SendAsync(Form(fields), host.SeedSession(), encrypted: true, cancellationToken);

        Assert.Equal((HttpStatusCode.BadRequest, 0L), (response.StatusCode, await world.BatchCountAsync()));
    }

    private static SubmissionServiceHolder NoKey() => new(1_000_000, _ => Task.FromResult<SubmissionServiceHolder.Loaded?>(null));

    private static Task<SessionedHost> StartAsync(
        SubmissionWorld world, CancellationToken cancellationToken, long maxBytes = 1_000_000, int granted = SubmissionWorld.Domain, SubmissionServiceHolder? holder = null) =>
        SessionedHost.StartAsync(
            services => services.AddSingleton(holder ?? SubmissionServiceHolder.Of(world.Service, maxBytes))
                .AddSingleton(new BatchRegister(world.DataSource))
                .AddSingleton(new IntakeDoor(world.DataSource))
                .AddSingleton<IGrantedDomains>(new GrantedTo(granted)),
            SubmissionsEndpoint.Map,
            cancellationToken);

    private static Dictionary<string, string?> Fields(string? origin, bool originUnknown = false) => new(StringComparer.Ordinal)
    {
        ["domain"] = SubmissionWorld.Domain.ToString(CultureInfo.InvariantCulture),
        ["declaredForm"] = "conversation",
        ["sourceFormat"] = SubmissionWorld.Format,
        ["origin"] = origin,
        ["originUnknown"] = originUnknown ? "true" : null,
    };

    [SuppressMessage("Reliability", "CA2000", Justification = "The form takes the parts it is given and lets go of them with itself.")]
    private static MultipartFormDataContent OnlyTheForm() => new() { { new StringContent("conversation"), "declaredForm" } };

    [SuppressMessage("Reliability", "CA2000", Justification = "The form takes the parts it is given and lets go of them with itself, and the request lets go of the form.")]
    private static HttpRequestMessage Form(Dictionary<string, string?> fields)
    {
        var form = new MultipartFormDataContent();
        foreach (var field in fields.Where(field => field.Value is not null))
        {
            form.Add(new StringContent(field.Value!), field.Key);
        }

        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(SubmissionWorld.Conversation)), "file", "conversa.json");
        return new HttpRequestMessage(HttpMethod.Post, new Uri(Submit, UriKind.Relative)) { Content = form };
    }

    private static async Task<string[]> FieldsOfAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return [.. json.RootElement.GetProperty("fields").EnumerateArray().Select(field => $"{field.GetProperty("field").GetString()}:{field.GetProperty("code").GetString()}")];
    }

    private static async Task<int> CountAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return json.RootElement.GetProperty("batches").GetArrayLength();
    }
}
