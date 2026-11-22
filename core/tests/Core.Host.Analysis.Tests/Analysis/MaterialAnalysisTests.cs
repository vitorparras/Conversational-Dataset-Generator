using Core.Boundary.Contracts;
using Core.Host.Analysis;

namespace Core.Host.Tests.Analysis;

/// <summary>
/// One analysis asks the recognizer once under a new reference: a concluded analysis comes back with the count of each
/// kind; an unavailable service comes back as a refusal with its cause and no analysis to keep, and is logged by reference;
/// a retry names the reference it resumes, and the log ties the two (issue #32, CA-2 and CA-5).
/// </summary>
public sealed class MaterialAnalysisTests
{
    private static readonly AnalysisAsked Asked = new("falei com PESSOA_1 ontem ZQX-MARKER-0027-FLOW", "pt");

    private readonly AnalysisLog log = new();

    public static TheoryData<UnavailabilityReason> Reasons => [.. Enum.GetValues<UnavailabilityReason>()];

    [Fact]
    public async Task ConcludedAnalysisCountsTheOccurrencesOfEachKindAsTheRecognizerWroteIt()
    {
        var recognizer = AnsweringRecognizer.Finding("PERSON", "zqx kind 7", "PERSON", "person");

        var result = await AnalyzeAsync(recognizer, Asked);

        Assert.Equal(4, result.Concluded!.Occurrences);
        Assert.Equal(new Dictionary<string, int> { ["PERSON"] = 2, ["person"] = 1, ["zqx kind 7"] = 1 }, result.Concluded.Kinds);
        Assert.Equal("zqx-model-0027", result.Concluded.ModelVersion);
    }

    [Fact]
    public async Task AnalysisThatFoundNothingIsConcludedWithNoOccurrence()
    {
        var result = await AnalyzeAsync(AnsweringRecognizer.Finding(), Asked);

        Assert.Equal(0, result.Concluded!.Occurrences);
        Assert.Empty(result.Concluded.Kinds);
        Assert.Null(result.Refused);
    }

    [Theory]
    [MemberData(nameof(Reasons))]
    public async Task UnavailableServiceIsARefusalWithTheReasonAndNoAnalysisToKeep(UnavailabilityReason reason)
    {
        var result = await AnalyzeAsync(AnsweringRecognizer.Unavailable(reason), Asked);

        Assert.Null(result.Concluded);
        Assert.Equal(reason, result.Refused!.Reason);
        Assert.StartsWith("Material não analisado e nada gravado: o serviço de linguagem ", result.Refused.Cause, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachReasonHasACauseOfItsOwn()
    {
        var timedOut = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.TimedOut), Asked);
        var refused = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.ConnectionRefused), Asked);
        var unknown = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.UnknownContractVersion), Asked);

        Assert.Contains("tempo limite", timedOut.Refused!.Cause, StringComparison.Ordinal);
        Assert.Contains("não pôde ser alcançado", refused.Refused!.Cause, StringComparison.Ordinal);
        Assert.Contains("versão do contrato", unknown.Refused!.Cause, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedResponseIsToldAsWhatAServiceThatDiesWithTheRequestInFlightLeaves()
    {
        var result = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.MalformedResponse), Asked);

        Assert.Contains("encerra com o pedido em curso", result.Refused!.Cause, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecognizerIsAskedOnceUnderTheReferenceTheResultCarries()
    {
        var recognizer = AnsweringRecognizer.Finding();

        var result = await AnalyzeAsync(recognizer, Asked);

        var request = Assert.Single(recognizer.Asked);
        Assert.Equal(result.Concluded!.Reference.ToString(), request.Reference);
        Assert.Equal((Asked.Text, "pt", ContractVersion.Current), (request.Text, request.Language, request.ContractVersion));
    }

    [Fact]
    public async Task EveryAnalysisHasAReferenceOfItsOwn()
    {
        var recognizer = AnsweringRecognizer.Finding();

        var first = await AnalyzeAsync(recognizer, Asked);
        var second = await AnalyzeAsync(recognizer, Asked);

        Assert.NotEqual(first.Concluded!.Reference, second.Concluded!.Reference);
    }

    [Fact]
    public async Task RefusalIsLoggedOnceWithItsReferenceAndReason()
    {
        var result = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.TimedOut), Asked);

        var line = Assert.Single(log.Of(204));
        Assert.StartsWith($"Análise {result.Refused!.Reference} interrompida: TimedOut; nada foi gravado", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcludedAnalysisAsksForNoLineOfItsOwn()
    {
        _ = await AnalyzeAsync(AnsweringRecognizer.Finding("PERSON"), Asked);

        Assert.Empty(log.Lines);
    }

    [Fact]
    public async Task RetryThatNamesTheInterruptedAnalysisIsLoggedWithBothReferences()
    {
        var interrupted = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.MalformedResponse), Asked);
        var interruptedReference = interrupted.Refused!.Reference;

        var resumed = await AnalyzeAsync(AnsweringRecognizer.Finding(), Asked with { Resumes = interruptedReference });

        var line = Assert.Single(log.Of(205));
        Assert.StartsWith(
            $"Análise {resumed.Concluded!.Reference} pedida para retomar a análise interrompida {interruptedReference}", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalysisThatResumesNothingWritesNoResumption()
    {
        _ = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.TimedOut), Asked);

        Assert.Empty(log.Of(205));
    }

    [Fact]
    public async Task NoLineHoldsTheTextAcrossARefusalAndItsResumption()
    {
        var interrupted = await AnalyzeAsync(AnsweringRecognizer.Unavailable(UnavailabilityReason.ConnectionRefused), Asked);

        _ = await AnalyzeAsync(AnsweringRecognizer.Finding("PERSON"), Asked with { Resumes = interrupted.Refused!.Reference });

        Assert.Equal(2, log.Lines.Count);
        Assert.DoesNotContain(log.Lines, line => line.Contains("ZQX-MARKER-0027", StringComparison.Ordinal));
        Assert.DoesNotContain(log.Lines, line => line.Contains("PESSOA_1", StringComparison.Ordinal));
    }

    [Fact]
    public void AskedAnalysisDescribesItselfWithoutTheText()
    {
        var described = Asked.ToString();

        Assert.DoesNotContain("ZQX-MARKER-0027", described, StringComparison.Ordinal);
    }

    private Task<AnalysisResult> AnalyzeAsync(IRecognizer recognizer, AnalysisAsked asked) =>
        new MaterialAnalysis(recognizer, log).AnalyzeAsync(asked, TestContext.Current.CancellationToken);
}
