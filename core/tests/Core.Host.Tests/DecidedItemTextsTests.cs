using Core.Boundary.Contracts;
using Core.Boundary.Fake;
using Core.Boundary.Text;
using Core.Host.Submissions;
using Core.Ingestion;
using Core.Ingestion.Deciding;
using Core.Persistence.Content;

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

        var texts = DecidedItemTexts.Of(decided, 0, Text, null, null);

        Assert.Equal(Text, texts.Raw);
        var stored = Assert.Single(texts.Occurrences!);
        Assert.Equal((10, 10 + Marker.Length, "PERSON", OccurrenceDestination.Pseudonymize), (stored.Start, stored.End, stored.Kind, stored.Destination));
    }

    [Fact]
    public async Task ATextWithNothingFoundIsStoredAsDecidedWithNoOccurrence()
    {
        var plain = "ZQX-FAKE-NOTHING sem ninguem";
        var decided = await DecidedAsync(plain);

        var texts = DecidedItemTexts.Of(decided, 0, plain, null, null);

        Assert.Empty(texts.Occurrences!);
    }

    [Fact]
    public void NoMaterialIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => DecidedItemTexts.Of(null!, 0, Text, null, null));

        Assert.Equal("material", defect.ParamName);
    }

    private static async Task<DecidedMaterial> DecidedAsync(string text)
    {
        var read = ReadOutcome.Read(new ReadMaterial([new ReadUnit(text, null, null, null, [])]));
        var material = Assert.IsAssignableFrom<Material>(new FormConference(new TextBoundaries()).Confer(MaterialForm.Document, read).Accepted);
        var request = new AnalysisRequest(text, "pt", "ZQX-REF-0001", ContractVersion.Current);
        var analysis = await new FakeRecognizer().RecognizeAsync(request, TestContext.Current.CancellationToken);

        return Assert.IsType<DecidedMaterial>(DecidedMaterial.Decide(material, [analysis]).Decided);
    }
}
