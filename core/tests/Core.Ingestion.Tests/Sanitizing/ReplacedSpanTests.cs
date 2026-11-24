using static Core.Ingestion.Tests.Sanitizing.Sanitizing;

namespace Core.Ingestion.Tests.Sanitizing;

/// <summary>
/// The span the text loses is exactly the one the recognizer pointed at, in code points, however many emoji, pairs and combining marks come before it; two occurrences where
/// one ends where the other starts are both replaced, and nothing of either stays.
/// </summary>
public sealed class ReplacedSpanTests
{
    [Fact]
    public void ReplacedSpanIsExactlyWhatTheRecognizerPointedPastEmojiAndCombiningMarks()
    {
        const string raw = "\U0001F600é\U0001F468‍\U0001F469 QX7 fim";
        var pointed = Data(7, 10);

        var sanitized = Sanitized(raw, pointed);

        Assert.Equal("QX7", Boundaries.Slice(raw, pointed.Occurrence));
        Assert.Equal("\U0001F600é\U0001F468‍\U0001F469 [dado] fim", sanitized.Text);
    }

    [Fact]
    public void AdjacentOccurrencesAreBothReplaced()
    {
        const string raw = "\U0001F600 QXZ7KW cd";

        var sanitized = Sanitized(raw, Data(2, 5), Credential(5, 8));

        Assert.Equal("\U0001F600 [dado][credencial] cd", sanitized.Text);
    }
}
