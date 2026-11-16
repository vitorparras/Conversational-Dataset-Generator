using System.Globalization;
using System.Text;
using Core.Host.Endpoints;
using Core.Host.Review;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Ingestion;
using Core.Persistence.People;
using Core.Platform.Keys;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the review by sample over the real thing (issue 822): the real routes of the batches and of the review behind the real gates, the real desk over the database of the run and
/// a synthetic key that seals and reads the text, and a batch each test opens by the real service of the submissions, so that the draw, the marks, the finding and the approval write
/// what the installation writes. The sample is declared with <see cref="Items"/> items, and every conversation has as many messages.
/// </summary>
internal static class SampleReviewRealCase
{
    /// <summary>How many items the sample has, which is also how many messages a conversation of the case has.</summary>
    public const int Items = 3;

    private static readonly ScreenParts Submission = SubmissionCase.Parts();

    /// <summary>The text of an item of a conversation made up for the run: it must appear beside the item and nowhere else.</summary>
    public static string TextOf(string tag, int position) => $"ZQXTEXTO{tag}ITEM{position.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The parts of the host: those of the submission, the desk of the review and every route of the batches and of the review.</summary>
    public static ScreenParts Parts() => Submission with
    {
        Services = (services, setup) =>
        {
            Submission.Services(services, setup);
            _ = services.AddSingleton(DeskOf);
        },
        Screens = BatchRoutes.Map,
    };

    /// <summary>Opens a batch with a conversation of its own, by the service the form of the submission uses, and answers its number; the batch has no sample drawn.</summary>
    /// <param name="host">The host of the run, whose curator is the author of the batch.</param>
    /// <param name="tag">What makes the conversation differ from every other, since a file already accepted is not received twice.</param>
    public static async Task<long> OpenBatchAsync(ScreenHost host, string tag)
    {
        var holder = host.Services.GetRequiredService<SubmissionServiceHolder>();
        using var loaded = await holder.GetAsync(CancellationToken.None) ?? throw new InvalidOperationException("The service of the submissions was not built.");
        var outcome = await loaded.Service.SubmitAsync(
            new SubmissionRequest
            {
                DomainId = await DomainAsync(host.Services.GetRequiredService<NpgsqlDataSource>()),
                AuthorId = host.CuratorId,
                DeclaredForm = MaterialForm.Conversation,
                SourceFormat = SubmissionCase.Format,
                Condition = "zqx_livre",
                Origin = "zqx-origem-do-teste",
                Content = new MemoryStream(Encoding.UTF8.GetBytes(ConversationOf(tag))),
                ReceivedName = "conversa.json",
            },
            CancellationToken.None);
        return outcome is { Kind: SubmissionKind.Done, Batch: { } batch } ? batch : throw new InvalidOperationException($"The batch was not opened: {outcome.Kind}.");
    }

    private static string ConversationOf(string tag) =>
        """{"formato":"cdg-conversa/1","mensagens":[""" +
        string.Join(",", Enumerable.Range(1, Items).Select(position =>
            $$"""{"id":"m{{position}}","remetente":"ZQX{{position % 2}}","instante":"2031-03-04T08:0{{position}}:00Z","texto":"{{TextOf(tag, position)}}"}""")) + "]}";

    private static async Task<int> DomainAsync(NpgsqlDataSource source)
    {
        await using var command = source.CreateCommand("SELECT id FROM public.domain WHERE name = @name");
        _ = command.Parameters.AddWithValue("name", SubmissionCase.Granted);
        return (int)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The domain of the case does not exist."));
    }

    /// <summary>The desk as the composition builds it, over the database and the key of the host, with the sample the case declares.</summary>
    private static SampleReviewDesk DeskOf(IServiceProvider provider)
    {
        var source = provider.GetRequiredService<NpgsqlDataSource>();
        var reader = CompositionRoot.SampleTextOf(source, provider.GetRequiredService<MasterKeyReading>(), new HashSet<string>(SubmissionCase.Labels, StringComparer.Ordinal));
        var batches = new IngestionBatches(source, OriginConditions.Of(["zqx_sigilosa", "zqx_livre"]));
        return new SampleReviewDesk(
            (new BatchReviews(source, Items), new SampleMarks(source), new CopyClosing(new BatchApprovals(source), batches)),
            (new PersonRegistry(source), _ => Task.FromResult(new SampleTextAccess(reader, null)), Items));
    }
}
