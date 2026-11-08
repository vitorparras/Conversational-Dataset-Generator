using System.Text;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// A migrated database of its own with two domains and a person, and the service of the submissions over it,
/// with a limit of bytes of the test and the gate open or closed as the test asks.
/// </summary>
internal sealed class SubmissionWorld : IAsyncDisposable
{
    internal const int Domain = 1;
    internal const int OtherDomain = 2;
    internal const long Person = 1;
    internal const string Strictest = "zqx_sigilosa";
    internal const string Loosest = "zqx_livre";
    internal const string Format = "cdg-conversa/1";

    internal const string Conversation =
        /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"ZQXUM","instante":"2031-03-04T08:00:00Z","texto":"ZQXPRIMEIRO"},""" +
        /*lang=json*/ """{"id":"b","remetente":"ZQXDOIS","instante":"2031-03-04T08:01:00Z","texto":"ZQXSEGUNDO"}]}""";

    private SubmissionWorld(ScratchDatabase database, NpgsqlDataSource dataSource, IngestionSubmissions service)
    {
        Database = database;
        DataSource = dataSource;
        Service = service;
    }

    internal ScratchDatabase Database { get; }

    internal NpgsqlDataSource DataSource { get; }

    internal IngestionSubmissions Service { get; }

    internal static async Task<SubmissionWorld> CreateAsync(long maxBytes = 1_000_000, bool gateOpen = true)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-active', 'Anotação')");
        var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        if (gateOpen)
        {
            var rehearsed = CustodyEvent.RecoveryRehearsed(1, new string('0', 64));
            Assert.Equal(CustodyOutcome.Appended, (await new CustodyChain(dataSource).AppendAsync(rehearsed, TestContext.Current.CancellationToken)).Outcome);
        }

        var conditions = OriginConditions.Of([Strictest, "zqx_media", Loosest]);
        var intake = Intake(maxBytes, conditions);
        return new SubmissionWorld(database, dataSource, new IngestionSubmissions(intake, new IngestionBatches(dataSource, conditions), new BatchRegister(dataSource), dataSource));
    }

    internal static IngestionIntake Intake(long maxBytes, OriginConditions conditions) =>
        new(new SubmissionGate(SourceReaders.Default(), maxBytes), new FormConference(new TextBoundaries()), conditions);

    internal static SubmissionRequest Request(string content = Conversation, string? name = "conversa.json", string? condition = null) => new()
    {
        DomainId = Domain,
        AuthorId = Person,
        DeclaredForm = MaterialForm.Conversation,
        SourceFormat = Format,
        Condition = condition,
        Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        ReceivedName = name,
    };

    internal Task<SubmissionOutcome> SubmitAsync(SubmissionRequest request) => Service.SubmitAsync(request, TestContext.Current.CancellationToken);

    internal async Task<long> BatchCountAsync()
    {
        var total = 0L;
        foreach (var domain in new[] { Domain, OtherDomain })
        {
            var entries = await Service.ListAsync(new BatchFilter { Domains = new[] { domain }, Take = 100 }, TestContext.Current.CancellationToken);
            total += entries.Count;
        }

        return total;
    }

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await Database.DisposeAsync();
    }
}
