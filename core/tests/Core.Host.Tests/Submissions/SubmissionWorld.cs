using System.Text;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
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
    internal const string Origin = "zqx-origem-secreta";

    internal const string Conversation =
        /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"ZQXUM","instante":"2031-03-04T08:00:00Z","texto":"ZQXPRIMEIRO"},""" +
        /*lang=json*/ """{"id":"b","remetente":"ZQXDOIS","instante":"2031-03-04T08:01:00Z","texto":"ZQXSEGUNDO"}]}""";

    private SubmissionWorld(ScratchDatabase database, NpgsqlDataSource dataSource, (SubmissionService Service, ProtectedContent Content) service)
    {
        Database = database;
        DataSource = dataSource;
        Service = service.Service;
        Content = service.Content;
        Reader = new ProtectedContentReader(dataSource, service.Content, new CustodyChain(dataSource));
    }

    internal ScratchDatabase Database { get; }

    internal NpgsqlDataSource DataSource { get; }

    internal SubmissionService Service { get; }

    internal ProtectedContent Content { get; }

    internal ProtectedContentReader Reader { get; }

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
        var content = SealingOf(["texto-bruto/1/v1", "texto-bruto/2/v1"]);
        var intake = Intake(maxBytes, conditions, content);
        var service = new SubmissionService(intake, new IngestionBatches(dataSource, conditions), new BatchRegister(dataSource), dataSource);
        return new SubmissionWorld(database, dataSource, (service, content));
    }

    /// <summary>The path of protected content over a synthetic key that holds the labels given, as the installation would load them.</summary>
    internal static ProtectedContent SealingOf(IReadOnlyList<string> labels)
    {
        var loaded = DerivationLabels.Of(labels);
        var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
        var secret = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + 11))).ToArray();
        var reading = MasterKeyReading.FromBytes(key, loaded, CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = secret }), "synthetic");
        return new ProtectedContent(loaded, new ItemKeys(new KeyDerivation(reading.Key!, loaded), reading.Epochs), new HashSet<string>(labels, StringComparer.Ordinal));
    }

    internal static SubmissionIntake Intake(long maxBytes, OriginConditions conditions, ProtectedContent content) =>
        new(new SubmissionGate(SourceReaders.Default(), maxBytes), new FormConference(new TextBoundaries()), conditions, content);

    internal static SubmissionRequest Request(string content = Conversation, string? name = "conversa.json", string? condition = null) => new()
    {
        DomainId = Domain,
        AuthorId = Person,
        DeclaredForm = MaterialForm.Conversation,
        SourceFormat = Format,
        Condition = condition,
        Origin = Origin,
        Content = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        ReceivedName = name,
    };

    internal Task<SubmissionOutcome> SubmitAsync(SubmissionRequest request) => Service.SubmitAsync(request, TestContext.Current.CancellationToken);

    internal async Task<long> BatchCountAsync()
    {
        var total = 0L;
        foreach (var domain in new[] { Domain, OtherDomain })
        {
            var entries = await Service.ListAsync(new BatchFilter { Domains = [domain], Take = 100 }, TestContext.Current.CancellationToken);
            total += entries.Count;
        }

        return total;
    }

    internal async Task<long> OriginRowsAsync()
    {
        var total = 0L;
        foreach (var domain in new[] { Domain, OtherDomain })
        {
            await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("SELECT set_config('app.domain', @domain, true)", connection, transaction);
            _ = command.Parameters.AddWithValue("domain", domain.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            command.CommandText = "SELECT count(*) FROM content.batch_origin";
            total += (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        }

        return total;
    }

    internal Task<IReadOnlyList<string>> OriginEventsAsync() => Database.FirstColumnAsOwnerAsync(
        "SELECT item_kind || ',' || item_id || ',' || step || ',' || reason FROM public.custody_event WHERE step = 'origin_read' ORDER BY sequence");

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await Database.DisposeAsync();
    }
}
