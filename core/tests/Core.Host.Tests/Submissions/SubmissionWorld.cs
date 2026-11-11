using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;
using Core.Persistence.Content;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Microsoft.Extensions.Logging.Abstractions;
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

    private SubmissionWorld(ScratchDatabase database, NpgsqlDataSource dataSource, (SubmissionService Service, ProtectedContent Content, IngestionBatches Batches) service)
    {
        Database = database;
        DataSource = dataSource;
        Service = service.Service;
        Content = service.Content;
        Batches = service.Batches;
        Reader = new ProtectedContentReader(dataSource, service.Content, new CustodyChain(dataSource));
    }

    internal ScratchDatabase Database { get; }

    internal NpgsqlDataSource DataSource { get; }

    internal SubmissionService Service { get; }

    internal ProtectedContent Content { get; }

    internal IngestionBatches Batches { get; }

    internal ProtectedContentReader Reader { get; }

    /// <summary>The name the world gave to the domain, as the grants name it.</summary>
    /// <param name="domain">The identifier of the domain.</param>
    internal static string NameOf(int domain) => domain == Domain ? "zqx-domain-a" : "zqx-domain-b";

    internal static async Task<SubmissionWorld> CreateAsync(long maxBytes = 1_000_000, bool gateOpen = true, IReadOnlyList<string>? labels = null, ReplicationRule? replication = null, int? attempts = null)
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
        var content = SealingOf(labels ?? ["texto-bruto/1/v1", "texto-bruto/2/v1"]);
        var intake = Intake(maxBytes, conditions, content, dataSource, replication);
        var batches = attempts is { } made ? new IngestionBatches(dataSource, conditions, made) : new IngestionBatches(dataSource, conditions);
        var service = new SubmissionService(intake, batches, dataSource, NullLogger.Instance);
        return new SubmissionWorld(database, dataSource, (service, content, batches));
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

    internal static SubmissionIntake Intake(long maxBytes, OriginConditions conditions, ProtectedContent content, NpgsqlDataSource dataSource, ReplicationRule? replication = null) =>
        new(
            new SubmissionGate(SourceReaders.Default(), maxBytes),
            new FormConference(new TextBoundaries()),
            conditions,
            content,
            new ContentWriter(dataSource, content),
            replication is null ? null : new ReplicationMarking(new ReplicationStore(dataSource), MarkingDerivation(), replication, new TextBoundaries()));

    private static KeyDerivation MarkingDerivation()
    {
        var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x3C ^ ((index * 17) + 5))).ToArray();
        return new KeyDerivation(MasterKeyReading.FromBytes(key, DerivationLabels.None).Key!, DerivationLabels.None);
    }

    /// <summary>A conversation of the format with one message per pair, in order, a minute apart from 08:00.</summary>
    internal static string Messages(params (string Sender, string Text)[] entries) =>
        """{"formato":"cdg-conversa/1","mensagens":[""" +
        string.Join(",", entries.Select((entry, at) => $$"""{"id":"m{{at}}","remetente":"{{entry.Sender}}","instante":"2031-03-04T08:0{{at}}:00Z","texto":"{{entry.Text}}"}""")) + "]}";

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

    internal Task<long> OriginRowsAsync() => RowsAsync("SELECT count(*) FROM content.batch_origin");

    internal Task<long> ItemRowsAsync() => RowsAsync("SELECT count(*) FROM content.item_text");

    internal Task<long> MarkRowsAsync() => RowsAsync("SELECT count(*) FROM content.replication_mark");

    internal Task<long> OccurrenceRowsAsync() => RowsAsync("SELECT count(*) FROM content.replication_occurrence");

    internal Task<long> AccommodatedRowsAsync() => RowsAsync("SELECT count(*) FROM content.item_text WHERE accommodated_into = 'document' AND losses = 'author,instant'");

    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The statements are constants of the tests, never input.")]
    private async Task<long> RowsAsync(string count)
    {
        var total = 0L;
        foreach (var domain in new[] { Domain, OtherDomain })
        {
            await using var connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await using var command = new NpgsqlCommand("SELECT set_config('app.domain', @domain, true)", connection, transaction);
            _ = command.Parameters.AddWithValue("domain", domain.ToString(System.Globalization.CultureInfo.InvariantCulture));
            _ = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            command.CommandText = count;
            total += (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
        }

        return total;
    }

    /// <summary>The first column of a query over content as the owner reads it, inside the domain of the tests; the sealed bytes come as the database holds them.</summary>
    /// <param name="select">The query, with no parameters.</param>
    /// <param name="domain">The domain the query runs in.</param>
    [SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The statements are constants of the tests, never input.")]
    internal async Task<IReadOnlyList<string>> StoredAsync(string select, int domain = Domain)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(Database.OwnerConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT set_config('app.domain', @domain, true)", connection, transaction);
        _ = command.Parameters.AddWithValue("domain", domain.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _ = await command.ExecuteScalarAsync(cancellationToken);
        command.CommandText = select;
        var found = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            found.Add(reader.GetString(0));
        }

        return found;
    }

    internal Task<IReadOnlyList<string>> OriginEventsAsync() => Database.FirstColumnAsOwnerAsync(
        "SELECT item_kind || ',' || item_id || ',' || step || ',' || reason FROM public.custody_event WHERE step = 'origin_read' ORDER BY sequence");

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await Database.DisposeAsync();
    }
}
