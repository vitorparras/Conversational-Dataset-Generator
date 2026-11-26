using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Custody.Verification;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// What the tests of the rerun share, in the domain of one test: items written into its accepted batch, the queue and the rerun over a clock the test moves, the rerun as the domain would answer it,
/// and the readings of conference, by the pieces of production where one exists and as the owner only for what none of them shows.
/// </summary>
internal sealed class RerunCase
{
    /// <summary>The steps the rerun reruns to, and their version: another list than the one the items were written with.</summary>
    public const string TargetSteps = "destinations@1\nzqx_rerun@1";

    /// <summary>A third list, for a request that comes after a change of the steps.</summary>
    public const string LaterSteps = "destinations@1\nzqx_rerun@2";

    /// <summary>The raw text of every item the case writes.</summary>
    public const string Raw = "texto bruto sintetico zqx";

    private readonly ScratchDatabase database;
    private readonly NpgsqlDataSource application;
    private readonly SyntheticCustody custody;

    internal RerunCase(ScratchDatabase database, NpgsqlDataSource application, SyntheticCustody custody, int domainId)
    {
        this.database = database;
        this.application = application;
        this.custody = custody;
        DomainId = domainId;
        Queue = new SanitizationReruns(application, Clock);
        Resanitizer = new ItemResanitizer(application, custody.Content, Clock);
    }

    public static string Target { get; } = VersionOf(TargetSteps);

    public static string Later { get; } = VersionOf(LaterSteps);

    public int DomainId { get; }

    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public SanitizationReruns Queue { get; }

    public ItemResanitizer Resanitizer { get; }

    /// <summary>The queue of another worker, over the same database and the same clock.</summary>
    public SanitizationReruns OtherQueue() => new(application, Clock);

    /// <summary>The version of a list of steps: the SHA-256 of the list, in lowercase hexadecimal.</summary>
    public static string VersionOf(string steps) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(steps)));

    /// <summary>What the domain answers for the item under the target: the text given, and whether it is the one the item holds.</summary>
    public static Func<RerunSource, Resanitized> Rerunning(string text, string? version = null) => source => new Resanitized(
        text,
        version ?? Target,
        version is null ? TargetSteps : ItemTexts.Steps,
        $$"""{"raw":{{source.Raw.Length}},"steps":[[],[]]}""",
        version is null ? [1, 0] : [0],
        version is null ? [0, 2] : [0],
        string.Equals(text, source.StoredText, StringComparison.Ordinal));

    /// <summary>An item of the accepted batch of the domain, or of the batch given, sanitized by the list the tests of the content write with.</summary>
    public Task<long> ItemAsync(string sanitized, long? batch = null) =>
        new ContentWriter(application, custody.Content).AddItemTextAsync(DomainId, batch ?? DomainId, ItemTexts.Of(Raw, sanitized), TestContext.Current.CancellationToken);

    /// <summary>An item of the accepted batch of the domain written before the destinations were decided: with no occurrences and no sanitization.</summary>
    public Task<long> UndecidedItemAsync() =>
        new ContentWriter(application, custody.Content).AddItemTextAsync(DomainId, DomainId, new NewItemText(Raw), TestContext.Current.CancellationToken);

    /// <summary>A pseudonymized item of the accepted batch of the domain, with its map and its markers, as the writer answers it: written, or the markers held already.</summary>
    public Task<PseudonymizedWrite> PseudonymizedAsync(NewItemText item, long? batch = null) =>
        new ContentWriter(application, custody.Content).AddPseudonymizedItemAsync(DomainId, batch ?? DomainId, item, TestContext.Current.CancellationToken);

    /// <summary>A second accepted batch of the domain, seeded by the owner as the first one is, for an item of another conversation.</summary>
    public async Task<long> AnotherBatchAsync()
    {
        await database.RunAsOwnerAsync(
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_is_born_open",
            "ALTER TABLE content.ingestion_batch DISABLE TRIGGER batch_open_needs_event",
            $"SELECT set_config('app.domain', '{DomainId}', false)",
            $"INSERT INTO content.ingestion_batch (domain_id, state, opened_by, condition, closed_at) SELECT {DomainId}, 'accepted', min(id), 'restrita', now() FROM public.person",
            "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_is_born_open",
            "ALTER TABLE content.ingestion_batch ENABLE ALWAYS TRIGGER batch_open_needs_event");
        return long.Parse(await OwnerTextAsync($"SELECT max(id)::text FROM content.ingestion_batch WHERE domain_id = {DomainId} AND id <> @item", DomainId), CultureInfo.InvariantCulture);
    }

    /// <summary>The markers the item holds, each with whether it stands for an author, then the ones the domain holds in the installation, read as the owner.</summary>
    public Task<string> MarkersOfAsync(long item) => OwnerTextAsync(
        "SELECT coalesce((SELECT string_agg(marker || ':' || author, ',' ORDER BY marker) FROM content.item_marker WHERE item_id = @item), '') || '|' || "
        + "coalesce((SELECT string_agg(marker, ',' ORDER BY marker) FROM content.marker_in_use WHERE domain_id = current_setting('app.domain')::int), '')",
        item);

    /// <summary>The request of the curator to rerun the domain to the steps given.</summary>
    public async Task<RerunResult> RequestAsync(string steps = TargetSteps, SanitizationReruns? queue = null) =>
        await (queue ?? Queue).RequestAsync(new RerunRequest(DomainId, await CuratorAsync(), VersionOf(steps), steps), TestContext.Current.CancellationToken);

    /// <summary>The item a worker of the target takes, which must be there.</summary>
    public async Task<RerunHold> TakenAsync()
    {
        var hold = await Queue.TakeAsync(DomainId, Target, TestContext.Current.CancellationToken);
        return Assert.NotNull(hold);
    }

    /// <summary>The progress of the request, which must be there.</summary>
    public async Task<RerunProgress> ProgressAsync(long rerunId) =>
        await Queue.ProgressAsync(DomainId, rerunId, TestContext.Current.CancellationToken) ?? throw new InvalidOperationException("The request was not found.");

    /// <summary>The sanitized text of the item, read by the reader of production.</summary>
    public async Task<string?> SanitizedAsync(long item) =>
        (await new ProtectedContentReader(application, custody.Content, new CustodyChain(application)).ReadAsync(
            new ContentRead(CustodyAuthor.System, ContentColumns.SanitizedText, DomainId, item), TestContext.Current.CancellationToken)).Text;

    /// <summary>The pseudonymized text of the item, read by the reader of production; none when it has none.</summary>
    public async Task<string?> PseudonymizedTextAsync(long item) =>
        (await new ProtectedContentReader(application, custody.Content, new CustodyChain(application)).ReadAsync(
            new ContentRead(CustodyAuthor.System, ContentColumns.PseudonymizedText, DomainId, item), TestContext.Current.CancellationToken)).Text;

    /// <summary>How many maps of the item are not emptied, then how many it ever had, read as the owner.</summary>
    public Task<string> MapsOfAsync(long item) => OwnerTextAsync(
        "SELECT count(*) FILTER (WHERE emptied_at IS NULL) || ',' || count(*) FROM content.reversal_map WHERE item_id = @item", item);

    /// <summary>The version the detail of the batch names, read by the counts of production.</summary>
    public async Task<string?> VersionAsync() =>
        (await new BatchCounts(application).ReadAsync(DomainId, DomainId, TestContext.Current.CancellationToken)).SanitizationVersion;

    /// <summary>Empties the item, by the curator.</summary>
    public async Task EmptyAsync(long item)
    {
        var emptied = await new ContentEmptier(application).EmptyAsync(
            new ItemEmptying(CustodyAuthor.Person(await CuratorAsync()), DomainId, item, EmptyingMotive.ContractRequirement, "PROT-2026/0846"), TestContext.Current.CancellationToken);
        Assert.Equal(EmptyingOutcome.Emptied, emptied.Outcome);
    }

    /// <summary>Declares lost the key the raw texts of the domain are sealed under.</summary>
    public async Task DeclareKeyLossAsync()
    {
        Assert.True(KeyLossScope.TryRead($"texto-bruto/{DomainId}/v1", out var scope));
        var declared = await new CustodyChain(application).AppendAsync(CustodyEvent.KeyLossDeclared(scope!, KeyLossAttempts.SafekeepingCopy), TestContext.Current.CancellationToken);
        Assert.Equal(CustodyOutcome.Appended, declared.Outcome);
    }

    /// <summary>The rerun over a custody that holds the labels given and nothing else, as an installation that did not load the others.</summary>
    public ItemResanitizer ResanitizerOver(SyntheticCustody other) => new(application, other.Content, Clock);

    /// <summary>
    /// Breaks the chain: an event changed outside the system and a verification that failed and was recorded, after which the chain refuses every event; the database is then of no use to
    /// another test.
    /// </summary>
    public async Task BreakChainAsync()
    {
        await database.ExecuteAsSuperuserAsync(
            "ALTER TABLE public.custody_event DISABLE TRIGGER custody_event_never_changed; UPDATE public.custody_event SET item_id = item_id + 1 WHERE sequence = 1; "
            + "ALTER TABLE public.custody_event ENABLE ALWAYS TRIGGER custody_event_never_changed");
        var failed = await new CustodyVerifier(application, TimeProvider.System).VerifyAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, failed.FirstInconsistent?.Sequence);
    }

    /// <summary>
    /// Makes the chain answer every event of a request as a serialization conflict, counting each try in a sequence that the rollback does not undo; the database is then of no use to
    /// another test.
    /// </summary>
    public Task RequestsAlwaysConflictAsync() => database.ExecuteAsSuperuserAsync(
        "CREATE SEQUENCE public.zqx_request_tries; "
        + "CREATE FUNCTION public.zqx_request_conflicts() RETURNS trigger LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog AS $f$ "
        + "BEGIN PERFORM pg_catalog.nextval('public.zqx_request_tries'); RAISE EXCEPTION 'conflict' USING ERRCODE = '40001'; END $f$; "
        + "CREATE TRIGGER zqx_request_conflicts BEFORE INSERT ON public.custody_event FOR EACH ROW WHEN (NEW.step = 'sanitization_rerun') "
        + "EXECUTE FUNCTION public.zqx_request_conflicts()");

    /// <summary>How many times a request tried to record its event since <see cref="RequestsAlwaysConflictAsync"/>, read as the superuser.</summary>
    public async Task<long> ConflictedRequestsAsync()
    {
        await using var connection = await Sql.OpenAsync(database.SuperuserConnectionString);
        await using var command = new NpgsqlCommand("SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM public.zqx_request_tries", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>Another writer of the chain, which takes the link the rerun read.</summary>
    public void AppendElsewhere()
    {
        var other = CustodyEvent.Of(CustodyAuthor.System, new CustodyItem(CustodyItemKind.Person, 1), CustodyStep.PersonCreated, CustodyReason.AdministrativeAct);
        Assert.Equal(CustodyOutcome.Appended, new CustodyChain(application).AppendAsync(other, TestContext.Current.CancellationToken).GetAwaiter().GetResult().Outcome);
    }

    /// <summary>The sanitization the row holds, as a digest of its columns, read as the owner: no reading of production shows the trail, the counts and the bytes together.</summary>
    public Task<string> SanitizationOfAsync(long item) => OwnerTextAsync(
        "SELECT md5(concat_ws('|', sanitized_text, sanitization_version, sanitization_trail, sanitization_counts::text, sanitization_untreated::text)) FROM content.item_text WHERE id = @item",
        item);

    /// <summary>The events of the rerun of the item, each as kind, step, reason, reference and whether the system wrote it, read as the owner.</summary>
    public Task<string> RerunEventsOfAsync(long item) => OwnerTextAsync(
        "SELECT coalesce(string_agg(item_kind || ',' || step || ',' || reason || ',' || reference || ',' || actor_system, ';' ORDER BY sequence), '') "
        + "FROM public.custody_event WHERE item_kind = 'item_text' AND item_id = @item AND step = 'item_resanitized'",
        item);

    /// <summary>The events of the request, each as kind, step, reason, reference and the name of who asked, read as the owner.</summary>
    public Task<string> RequestEventsOfAsync(long rerunId) => OwnerTextAsync(
        "SELECT coalesce(string_agg(e.item_kind || ',' || e.step || ',' || e.reason || ',' || e.reference || ',' || p.name, ';' ORDER BY e.sequence), '') "
        + "FROM public.custody_event e JOIN public.person p ON p.id = e.actor_person_id WHERE e.item_kind = 'sanitization_rerun' AND e.item_id = @item",
        rerunId);

    /// <summary>Runs the statement over the item as the application, under the domain, in a transaction that begins with the statement before, and commits.</summary>
    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of the calling tests, never input.")]
    public async Task AsApplicationAsync(string before, string statement, long item)
    {
        await using var transaction = await ContentTransaction.BeginAsync(application, DomainId, TestContext.Current.CancellationToken);
        foreach (var text in new[] { before, statement }.Where(text => text.Length > 0))
        {
            await using var command = transaction.CreateCommand(text);
            _ = command.Parameters.AddWithValue("item", item);
            _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A transaction of another request, left open, that queued the item to the version the items hold, as a second worker of the curation would.</summary>
    public async Task<(ContentTransaction Transaction, long RerunId)> QueuedElsewhereAsync(long item)
    {
        var transaction = await ContentTransaction.BeginAsync(application, DomainId, TestContext.Current.CancellationToken);
        await using var request = transaction.CreateCommand(
            "WITH rerun AS (INSERT INTO content.sanitization_rerun (domain_id, target_version, requested_by) VALUES (@domain, @version, @person) RETURNING id) "
            + "INSERT INTO content.sanitization_rerun_item (domain_id, rerun_id, item_id) SELECT @domain, id, @item FROM rerun RETURNING rerun_id");
        _ = request.Parameters.AddWithValue("domain", DomainId);
        _ = request.Parameters.AddWithValue("version", ItemTexts.Version);
        _ = request.Parameters.AddWithValue("person", await CuratorAsync());
        _ = request.Parameters.AddWithValue("item", item);
        return (transaction, (long)(await request.ExecuteScalarAsync(TestContext.Current.CancellationToken))!);
    }

    /// <summary>A transaction of another worker, left open, that locks every row of the queue of the domain, so that a statement over them waits until it ends.</summary>
    public async Task<ContentTransaction> QueueLockedAsync()
    {
        var transaction = await ContentTransaction.BeginAsync(application, DomainId, TestContext.Current.CancellationToken);
        await using var locking = transaction.CreateCommand("SELECT count(*) FROM (SELECT 1 FROM content.sanitization_rerun_item FOR UPDATE) AS locked");
        _ = await locking.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return transaction;
    }

    /// <summary>Waits until a session waits on a lock, failing after thirty seconds, or with the failure of the request when it ends without waiting.</summary>
    public async Task UntilSomeoneWaitsAsync(Task asked)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!await SomeoneWaitsAsync())
        {
            if (asked.IsCompleted)
            {
                Assert.Fail($"The request ended without waiting on the lock: {asked.Exception?.GetBaseException().ToString() ?? asked.Status.ToString()}");
            }

            Assert.True(DateTimeOffset.UtcNow < deadline, "The request never waited on the lock.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Whether a session of this database waits for a lock, as a statement held by an open transaction does; seen as the superuser, who sees what every session waits for.</summary>
    private async Task<bool> SomeoneWaitsAsync()
    {
        await using var connection = await Sql.OpenAsync(database.SuperuserConnectionString);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_catalog.pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))! > 0;
    }

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of this class, never input.")]
    private async Task<string> OwnerTextAsync(string statement, long item)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using (var scope = new NpgsqlCommand("SELECT set_config('app.domain', @domain, false)", connection))
        {
            _ = scope.Parameters.AddWithValue("domain", DomainId.ToString(CultureInfo.InvariantCulture));
            _ = await scope.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        }

        await using var command = new NpgsqlCommand(statement, connection);
        _ = command.Parameters.AddWithValue("item", item);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        return reader.GetString(0);
    }

    private async Task<long> CuratorAsync()
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand("SELECT id FROM public.person WHERE name = 'zqx-curator'", connection);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
