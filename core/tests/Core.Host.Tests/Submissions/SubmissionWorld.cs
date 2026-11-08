using System.Diagnostics.CodeAnalysis;
using System.Text;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Entrance;
using Core.Ingestion.Reading;
using Core.Persistence.Ingestion;
using Core.Persistence.Migrations;
using Core.Persistence.Tests.Fixtures;
using Npgsql;
using NpgsqlTypes;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// A migrated database of its own with two domains and a person, and the service of the submissions over it,
/// with a limit of bytes of the test.
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

    private SubmissionWorld(ScratchDatabase database, NpgsqlDataSource dataSource, SubmissionService service)
    {
        Database = database;
        DataSource = dataSource;
        Service = service;
    }

    internal ScratchDatabase Database { get; }

    internal NpgsqlDataSource DataSource { get; }

    internal SubmissionService Service { get; }

    internal static async Task<SubmissionWorld> CreateAsync(long maxBytes = 1_000_000)
    {
        var database = await ScratchDatabase.CreateAsync();
        _ = await database.MigrateAsync(EmbeddedMigrations.Load());
        await database.RunAsOwnerAsync(
            "INSERT INTO public.domain (name) VALUES ('zqx-domain-a'), ('zqx-domain-b')",
            "INSERT INTO public.person (name, function_name) VALUES ('zqx-active', 'Anotação')");
        var dataSource = NpgsqlDataSource.Create(database.AppConnectionString);
        var conditions = OriginConditions.Of([Strictest, "zqx_media", Loosest]);
        var intake = Intake(maxBytes, conditions);
        return new SubmissionWorld(database, dataSource, new SubmissionService(intake, new IngestionBatches(dataSource, conditions)));
    }

    internal static SubmissionIntake Intake(long maxBytes, OriginConditions conditions) =>
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

    /// <summary>The batches of the domain, or of both when none is given, newest first, as the superuser reads them (it is not held to the row policy).</summary>
    internal Task<IReadOnlyList<BatchView>> BatchesAsync(int? domain = null) =>
        ReadAsync("WHERE @domain::integer IS NULL OR domain_id = @domain", new NpgsqlParameter("domain", NpgsqlDbType.Integer) { Value = domain is { } given ? given : DBNull.Value });

    /// <summary>One batch with what its submission brought, or null when there is none.</summary>
    internal async Task<BatchView?> BatchAsync(long batch) =>
        (await ReadAsync("WHERE id = @id", new NpgsqlParameter("id", NpgsqlDbType.Bigint) { Value = batch })).SingleOrDefault();

    internal async Task<long> BatchCountAsync() => (await BatchesAsync()).Count;

    public async ValueTask DisposeAsync()
    {
        await DataSource.DisposeAsync();
        await Database.DisposeAsync();
    }

    [SuppressMessage("Security", "CA2100", Justification = "The filter is a literal of the callers above, never input.")]
    private async Task<IReadOnlyList<BatchView>> ReadAsync(string filter, NpgsqlParameter parameter)
    {
        await using var connection = new NpgsqlConnection(Database.SuperuserConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            // nosemgrep: sql-concatenation-csharp joins one of two literal filters of the callers above, never input.
            "SELECT id, domain_id, state, motive, opened_by, opened_at, submission, received_bytes, received_digest, warning FROM content.ingestion_batch " + filter + " ORDER BY id DESC",
            connection);
        _ = command.Parameters.Add(parameter);
        var views = new List<BatchView>();
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            views.Add(new BatchView(
                reader.GetInt64(0),
                reader.GetInt32(1),
                Enum.Parse<BatchState>(reader.GetString(2), ignoreCase: true),
                reader[3] as string,
                reader.GetInt64(4),
                reader.GetFieldValue<DateTimeOffset>(5),
                reader[6] is string submission ? new SubmissionReceipt(submission, reader.GetInt64(7), reader.GetString(8), reader[9] as string) : null));
        }

        return views;
    }
}

/// <summary>A batch as the test reads it from the table: its state, why it was rejected, who opened it and when, and the receipt of the submission.</summary>
internal sealed record BatchView(long Id, int DomainId, BatchState State, string? Reason, long SubmittedBy, DateTimeOffset SubmittedAt, SubmissionReceipt? Receipt);
