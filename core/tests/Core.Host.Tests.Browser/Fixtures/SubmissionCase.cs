using Core.Boundary.Contracts;
using Core.Host.Endpoints;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Custody;
using Core.Persistence.Custody.Chain;
using Core.Persistence.Domains;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace Core.Host.Tests.Browser.Fixtures;

/// <summary>
/// The case of the submission and of the batch (issue 821): the real routes of the batches and the real service of the submissions over a synthetic key, one domain granted to everyone who
/// enters, the recovery rehearsed so the ingestion is open, the format of the conversation and two conditions.
/// </summary>
internal static class SubmissionCase
{
    /// <summary>The domain granted, the only one the form offers.</summary>
    public const string Granted = "ZQX-dominio-envio";

    /// <summary>The format the gate reads, the only one the form offers.</summary>
    public const string Format = "cdg-conversa/1";

    /// <summary>A conversation of the format with two messages of two senders.</summary>
    public const string Conversation =
        /*lang=json*/ """{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"ZQXUM","instante":"2031-03-04T08:00:00Z","texto":"ZQXPRIMEIRO"},""" +
        /*lang=json*/ """{"id":"b","remetente":"ZQXDOIS","instante":"2031-03-04T08:01:00Z","texto":"ZQXSEGUNDO"}]}""";

    private const long MaxBytes = 1_000_000;
    private const int MinLength = 24;

    internal static readonly string[] Labels = ["texto-bruto/1/v1", "texto-bruto/2/v1"];

    /// <summary>The parts of the host, whose service of the submissions asks the recognizer given or, without one, one that finds nothing.</summary>
    public static ScreenParts Parts(IRecognizer? recognizer = null) => new()
    {
        Services = (services, _) => services
            .AddSingleton<DomainGrants>(new FirstGranted())
            .AddSingleton(provider => new DomainListing(provider.GetRequiredService<NpgsqlDataSource>()))
            .AddSingleton(provider => new BatchRegister(provider.GetRequiredService<NpgsqlDataSource>()))
            .AddSingleton(provider => new BatchCounts(provider.GetRequiredService<NpgsqlDataSource>()))
            .AddSingleton(_ => SyntheticReading())
            .AddSingleton(provider => SubmissionServiceHolder.Of(
                ServiceOf(provider.GetRequiredService<NpgsqlDataSource>(), provider.GetRequiredService<MasterKeyReading>(), recognizer ?? new CleanRecognizer()), MaxBytes)),
        Screens = BatchScreensEndpoint.Map,
        Prepare = SeedAsync,
    };

    /// <summary>The key of the tests, which the container keeps and disposes with the host, as the installation keeps the reading of the master key.</summary>
    private static MasterKeyReading SyntheticReading()
    {
        var key = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0x5A ^ ((index * 29) + 3))).ToArray();
        var secret = Enumerable.Range(0, MasterKey.Length).Select(index => (byte)(0xA7 ^ ((index * 31) + 11))).ToArray();
        return MasterKeyReading.FromBytes(key, DerivationLabels.Of(Labels), CustodyEpochs.Of(new Dictionary<int, byte[]> { [1] = secret }), "synthetic");
    }

    /// <summary>The service of the submissions as the composition builds it, over the conditions of the tests, the most restrictive first.</summary>
    private static SubmissionService ServiceOf(NpgsqlDataSource source, MasterKeyReading reading, IRecognizer recognizer)
    {
        var conditions = OriginConditions.Of(["zqx_sigilosa", "zqx_livre"]);
        var batches = new IngestionBatches(source, conditions);
        var registered = new HashSet<string>(Labels, StringComparer.Ordinal);
        var settings = new SubmissionSettings(
            batches, conditions, MaxBytes, registered, new HashSet<string>(StringComparer.Ordinal), new ReplicationRule(MinLength, ReplicationRule.FewestSenders),
            recognizer);
        return new SubmissionService(CompositionRoot.IntakeOf(source, reading, settings), batches, source, NullLogger.Instance);
    }

    /// <summary>The domain and the rehearsal of the recovery, the event that opens the ingestion.</summary>
    private static async Task SeedAsync(ScratchDatabase database)
    {
        await database.RunAsOwnerAsync($"INSERT INTO public.domain (name) VALUES ('{Granted}')");
        await using var source = NpgsqlDataSource.Create(database.AppConnectionString);
        var rehearsed = await new CustodyChain(source).AppendAsync(CustodyEvent.RecoveryRehearsed(1, new string('0', 64)), CancellationToken.None);
        if (rehearsed.Outcome != CustodyOutcome.Appended)
        {
            throw new InvalidOperationException($"The rehearsal of the recovery was not appended: {rehearsed.Outcome}.");
        }
    }

    /// <summary>A recognizer that finds nothing in any text, so every submission of the screens is kept as the composition keeps a clean one.</summary>
    private sealed class CleanRecognizer : IRecognizer
    {
        public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(RecognitionOutcome.Analyzed(new AnalysisResponse([], "zqx-model-clean")));
    }

    /// <summary>A recognizer that finds a credential in the first character of every text, which the destinations remove and the sanitized text shows by its marker.</summary>
    internal sealed class FirstCharacterCredential : IRecognizer
    {
        public Task<RecognitionOutcome> RecognizeAsync(AnalysisRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(RecognitionOutcome.Analyzed(new AnalysisResponse([new Occurrence(0, 1, "CREDENTIAL", "PROPN", "nsubj")], "zqx-model-credential")));
    }

    /// <summary>The domain granted to whoever enters, whatever the function; the function still decides what the gate opens.</summary>
    private sealed class FirstGranted : DomainGrants
    {
        internal override Task<IEnumerable<string>> GrantedToAsync(long entered, CancellationToken cancellationToken) => Task.FromResult(Enumerable.Repeat(Granted, 1));
    }
}
