using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Deciding;
using Core.Ingestion.Sanitizing;
using Core.Persistence.Content;
using Core.Persistence.Content.Rerun;

namespace Core.Host.Tests;

/// <summary>
/// The texts of an item are built from the material the destinations were decided for, with the fake recognizer and the policy as they are:
/// what is stored is what was found, with the destination the policy chose and the offsets as they crossed the boundary (issue 84, CA-1).
/// </summary>
public sealed class DecidedItemTextsTests
{
    private const string Marker = "ZQX-FAKE-PERSON";
    private const string Text = "Falei com " + Marker + " ontem";

    [Fact]
    public async Task TheOccurrencesStoredAreTheOnesTheRecognizerFoundWithTheDestinationThePolicyChose()
    {
        var decided = await DecidedAsync(Text);

        var texts = DecidedItemTexts.Of(decided, 0, Text, Sanitized(decided, Text));

        Assert.Equal(Text, texts.Raw);
        var stored = Assert.Single(texts.Occurrences!);
        Assert.Equal((10, 10 + Marker.Length, "PERSON", OccurrenceDestination.Pseudonymize), (stored.Start, stored.End, stored.Kind, stored.Destination));
    }

    [Fact]
    public async Task ATextWithNothingFoundIsStoredAsDecidedWithNoOccurrence()
    {
        var plain = "ZQX-FAKE-NOTHING sem ninguem";
        var decided = await DecidedAsync(plain);

        var texts = DecidedItemTexts.Of(decided, 0, plain, Sanitized(decided, plain));

        Assert.Empty(texts.Occurrences!);
    }

    [Fact]
    public void AnItemWhoseDestinationsWereNeverDecidedIsNotRerun() =>
        Assert.Null(DecidedItemTexts.Rerun(new SanitizationRerun(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries())), new RerunSource(Text, null, Text, null)));

    [Fact]
    public async Task NoMaterialIsADefect()
    {
        var decided = await DecidedAsync(Text);
        var sanitized = Sanitized(decided, Text);

        var defect = Assert.Throws<ArgumentNullException>(() => DecidedItemTexts.Of(null!, 0, Text, sanitized));

        Assert.Equal("material", defect.ParamName);
    }

    [Fact]
    public async Task NoSanitizedTextIsADefect()
    {
        var decided = await DecidedAsync(Text);

        var defect = Assert.Throws<ArgumentNullException>(() => DecidedItemTexts.Of(decided, 0, Text, null!));

        Assert.Equal("clean", defect.ParamName);
    }

    [Fact]
    public async Task TheItemCarriesTheSanitizedTextWithTheVersionAndOneCountForEachDeclaredStep()
    {
        var decided = await DecidedAsync(Text);

        var sanitization = DecidedItemTexts.Of(decided, 0, Text, Sanitized(decided, Text)).Sanitization;

        Assert.NotNull(sanitization);
        Assert.Equal((Text, CompositionRoot.StepsOf().Version, "codificacao@1\nartefatos@1\nidentificadores@1\ndestinos@1"), (sanitization.Text, sanitization.Version, sanitization.Steps));
        Assert.Equal([0, 0, 0, 0], sanitization.Counts);
        Assert.Equal(Text.Length, TrailJson.Read(sanitization.Trail).RawLength);
    }

    private static SanitizedText Sanitized(DecidedMaterial decided, string text) =>
        Assert.IsType<SanitizedText>(new Sanitizer(CompositionRoot.StepsOf(), new TextBoundaries()).Sanitize(text, decided.Of(0)).Sanitized);

    private static async Task<DecidedMaterial> DecidedAsync(string text)
    {
        var read = ReadOutcome.Read(new ReadMaterial([new ReadUnit(text, null, null, null, [])]));
        var material = Assert.IsAssignableFrom<Material>(new FormConference(new TextBoundaries()).Confer(MaterialForm.Document, read).Accepted);
        var request = new AnalysisRequest(text, "pt", "ZQX-REF-0001", ContractVersion.Current);
        var analysis = await new FakeRecognizer().RecognizeAsync(request, TestContext.Current.CancellationToken);

        return Assert.IsType<DecidedMaterial>(DecidedMaterial.Decide(material, [analysis]).Decided);
    }
}
