using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using Core.Persistence.Content;
using Core.Persistence.Content.Emptying;
using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Rerun;
using Core.Persistence.Content.Writing;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
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

    private const string Raw = "texto bruto sintetico zqx";

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

    /// <summary>An item of the accepted batch of the domain, sanitized by the list the tests of the content write with.</summary>
    public Task<long> ItemAsync(string sanitized) =>
        new ContentWriter(application, custody.Content).AddItemTextAsync(DomainId, DomainId, ItemTexts.Of(Raw, sanitized), TestContext.Current.CancellationToken);

    /// <summary>An item of the accepted batch of the domain written before the destinations were decided: with no occurrences and no sanitization.</summary>
    public Task<long> UndecidedItemAsync() =>
        new ContentWriter(application, custody.Content).AddItemTextAsync(DomainId, DomainId, new NewItemText(Raw), TestContext.Current.CancellationToken);

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

    [SuppressMessage("Security", "CA2100", Justification = "The statements are literals of this class, never input.")]
    private async Task<string> OwnerTextAsync(string statement, long item)
    {
        await using var connection = await Sql.OpenAsync(database.OwnerConnectionString);
        await using var command = new NpgsqlCommand($"SELECT set_config('app.domain', '{DomainId}', false); {statement}", connection);
        _ = command.Parameters.AddWithValue("item", item);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        _ = await reader.NextResultAsync(TestContext.Current.CancellationToken);
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
