using System.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Persistence.Ingestion;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Submissions;

/// <summary>
/// The service of the submissions (issue 75, criteria 1 to 5, from end to end): what is accepted, what is turned back and why,
/// what is recorded in each case, and what is never recorded.
/// </summary>
public sealed class IngestionSubmissionsTests
{
    [DatabaseFact]
    public async Task ValidFileIsAcceptedAndKeptInAnOpenBatchUnderTheMostRestrictiveConditionWithWhatArrived()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var bytes = Encoding.UTF8.GetBytes(SubmissionWorld.Conversation);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var entry = await world.BatchAsync(outcome.Batch!.Value);

        Assert.Equal(
            (SubmissionKind.Done, SubmissionState.Accepted, null, (long)bytes.Length, Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes))),
            (outcome.Kind, outcome.State, outcome.Reason, outcome.Size, outcome.Sha256));
        Assert.Equal(
            (BatchState.Open, SubmissionWorld.Person, outcome.Submission, outcome.Size, outcome.Sha256, null),
            (entry!.State, entry.SubmittedBy, entry.Receipt!.Submission, entry.Receipt.Bytes, entry.Receipt.Digest, entry.Receipt.Warning));
        Assert.Equal(32, outcome.Submission!.Length);
    }

    [DatabaseFact]
    public async Task ConditionDeclaredIsKeptAndTheOmittedOneTakesTheFirstOfTheConfiguredList()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var declared = await world.SubmitAsync(SubmissionWorld.Request(condition: SubmissionWorld.Loosest));
        var omitted = await world.SubmitAsync(SubmissionWorld.Request());

        var batches = new IngestionBatches(world.DataSource, OriginConditions.Of([SubmissionWorld.Strictest, "zqx_media", SubmissionWorld.Loosest]));
        var read = async (SubmissionOutcome outcome) => (await batches.ReadAsync(SubmissionWorld.Domain, outcome.Batch!.Value, TestContext.Current.CancellationToken))!.Condition;

        Assert.Equal((SubmissionWorld.Loosest, SubmissionWorld.Strictest), (await read(declared), await read(omitted)));
    }

    [DatabaseFact]
    public async Task FilePastTheLimitIsRecordedAsRejectedWithTheReasonAndIsTheCaseOf413()
    {
        await using var world = await SubmissionWorld.CreateAsync(maxBytes: 20);

        var outcome = await world.SubmitAsync(SubmissionWorld.Request());
        var entry = await world.BatchAsync(outcome.Batch!.Value);

        Assert.Equal(
            (SubmissionKind.Done, SubmissionState.Rejected, "too_large", true, 21L),
            (outcome.Kind, outcome.State, outcome.Reason!.Code, outcome.IsTooLarge, outcome.Size));
        Assert.Equal((BatchState.Rejected, "too_large", 21L), (entry!.State, entry.Reason, entry.Receipt!.Bytes));
    }

    [DatabaseTheory]
    [InlineData("conversa.json")]
    [InlineData("conversa.csv")]
    [InlineData("conversa")]
    [InlineData(null)]
    public async Task ContentThatDoesNotConfirmTheFormatIsRefusedWhateverTheNameOrTheExtensionSays(string? name)
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request("a,b\n1,2\n", name));

        Assert.Equal((SubmissionState.Rejected, "format_not_confirmed"), (outcome.State, outcome.Reason!.Code));
    }

    [DatabaseTheory]
    [InlineData("""{"formato":"cdg-conversa/1","mensagens":[{"id":"http://externo.example/x","remetente":"a","instante":"2031-03-04T08:00:00Z","texto":"t"}]}""")]
    [InlineData("""{"formato":"cdg-conversa/1","mensagens":[{"id":"a","remetente":"/etc/passwd","instante":"2031-03-04T08:00:00Z","texto":"t"}]}""")]
    public async Task FileThatPointsOutsideItselfIsRefusedAsUnreadableAndTheReferenceIsNotFollowed(string content)
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(content));

        Assert.Equal((SubmissionState.Rejected, "unreadable"), (outcome.State, outcome.Reason!.Code));
    }

    [DatabaseFact]
    public async Task FileThatExpandsPastTheDepthOfTheFormatIsRefusedWithAReasonInsteadOfBeingRead()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var deep = $$"""{"formato":"cdg-conversa/1","mensagens":{{new string('[', 200)}}{{new string(']', 200)}}}""";

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(deep));

        Assert.Equal((SubmissionState.Rejected, "unreadable"), (outcome.State, outcome.Reason!.Code));
    }

    [DatabaseFact]
    public async Task MaterialThatDoesNotHoldUpToTheFormDeclaredIsRejectedWithTheCodeOfTheConference()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var request = SubmissionWorld.Request() with { DeclaredForm = MaterialForm.Comment };

        var outcome = await world.SubmitAsync(request);
        var entry = await world.BatchAsync(outcome.Batch!.Value);

        Assert.Equal((SubmissionState.Rejected, "conversation_thread", BatchState.Rejected), (outcome.State, outcome.Reason!.Code, entry!.State));
        Assert.Equal(outcome.Reason.Code, entry.Reason);
    }

    [DatabaseTheory]
    [InlineData("../../etc/passwd")]
    [InlineData("C:\\Users\\x\\conversa.json")]
    [InlineData("/var/tmp/conversa.json")]
    [InlineData("a\u0001b.json")]
    public async Task NameWithAPathIsDiscardedWithAWarningByCodeAndTheFileIsNotRefusedForIt(string name)
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(name: name));
        var entry = await world.BatchAsync(outcome.Batch!.Value);

        Assert.Equal((SubmissionState.Accepted, "name_with_path", "name_with_path"), (outcome.State, outcome.Warning, entry!.Receipt!.Warning));
    }

    [DatabaseFact]
    public async Task TheNameTheFileCameWithIsInNoPlaceThatKeepsOrAnswersAnything()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        const string Name = "zqxsegredo/../nome.json";

        var accepted = await world.SubmitAsync(SubmissionWorld.Request(name: Name));
        var rejected = await world.SubmitAsync(SubmissionWorld.Request("nada", Name));
        var kept = await world.Database.DumpAsync();

        Assert.DoesNotContain("zqxsegredo", kept, StringComparison.Ordinal);
        Assert.DoesNotContain("zqxsegredo", $"{accepted}{rejected}", StringComparison.Ordinal);
    }

    [DatabaseFact]
    public async Task PlainNameRaisesNoWarning()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request(name: "conversa.json"));

        Assert.Null(outcome.Warning);
    }

    [DatabaseFact]
    public async Task EverySubmissionIsRecordedTheRefusedAndTheOneTooLargeIncluded()
    {
        await using var world = await SubmissionWorld.CreateAsync(maxBytes: 300);
        var accepted = await world.SubmitAsync(SubmissionWorld.Request());
        var turnedBack = await world.SubmitAsync(SubmissionWorld.Request("a,b\n"));
        var tooLarge = await world.SubmitAsync(SubmissionWorld.Request(new string('x', 1_000)));

        var listed = await world.BatchesAsync(SubmissionWorld.Domain);

        Assert.Equal(
            [tooLarge.Batch, turnedBack.Batch, accepted.Batch],
            listed.Select(entry => (long?)entry.Id));
        Assert.All(listed, entry => Assert.Equal((SubmissionWorld.Person, true), (entry.SubmittedBy, entry.SubmittedAt > DateTimeOffset.UtcNow.AddMinutes(-5))));
        Assert.All(listed, entry => Assert.Equal(64, entry.Receipt!.Digest.Length));
    }

    [DatabaseFact]
    public async Task RequestWithAMissingOrInvalidFieldRecordsNothingAndReadsNoByte()
    {
        await using var world = await SubmissionWorld.CreateAsync();
        var request = SubmissionWorld.Request(condition: "zqx_desconhecida") with { DomainId = 0, DeclaredForm = (MaterialForm)99, SourceFormat = " " };

        var outcome = await world.SubmitAsync(request);

        Assert.Equal(
            (SubmissionKind.Invalid, "domain:missing,declared_form:invalid,source_format:missing,condition:unknown", 0L, 0L),
            (outcome.Kind, string.Join(',', outcome.Fields!.Select(field => $"{field.Field}:{field.Code}")), request.Content.Position, await world.BatchCountAsync()));
        Assert.Null(outcome.Batch);
    }

    [DatabaseFact]
    public async Task DomainThatDoesNotExistIsNotRecordedAndTheOutcomeSaysWhy()
    {
        await using var world = await SubmissionWorld.CreateAsync();

        var outcome = await world.SubmitAsync(SubmissionWorld.Request() with { DomainId = 99 });

        Assert.Equal((SubmissionKind.NotRecorded, BatchOutcome.DomainUnknown, null), (outcome.Kind, outcome.Failure, outcome.Batch));
    }
}
