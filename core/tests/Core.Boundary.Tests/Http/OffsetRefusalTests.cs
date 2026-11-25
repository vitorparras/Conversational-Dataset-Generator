using System.Text;
using Core.Boundary.Contracts;
using Core.Boundary.Http;

namespace Core.Boundary.Tests.Http;

/// <summary>
/// An answer whose offset falls outside the text sent, or whose end comes before its start, is refused whole by the same path, and never cut to what fits; two occurrences
/// where one ends exactly where the other starts are adjacent, not crossed, and both pass.
/// </summary>
public sealed class OffsetRefusalTests : IDisposable
{
    private readonly Recognition recognition = new();

    [Fact]
    public void OffsetOutsideTheSentTextIsRefused()
    {
        var validation = Validate(Answer.WithOccurrences(0, 2, 3, Answer.TextCodePoints + 1));

        Assert.Equal((null, ValidationProblem.OffsetOutsideText), (validation.Response, validation.Problem));
    }

    [Fact]
    public async Task EndBeforeStartIsRefusedTheSameWay()
    {
        using var outside = FakeService.Answering(Reply.Ok(Body(0, Answer.TextCodePoints + 1)));
        using var inverted = FakeService.Answering(Reply.Ok(Body(5, 4)));

        var refusedOutside = await recognition.Of(outside.Port, Recognition.Generous).RecognizeAsync(Answer.RequestFor(Answer.Text), TestContext.Current.CancellationToken);
        var refusedInverted = await recognition.Of(inverted.Port, Recognition.Generous).RecognizeAsync(Answer.RequestFor(Answer.Text), TestContext.Current.CancellationToken);

        Assert.Equal(UnavailabilityReason.MalformedResponse, refusedOutside.Reason);
        Assert.Null(refusedOutside.Response);
        Assert.Equal((refusedOutside.Reason, refusedOutside.Response), (refusedInverted.Reason, refusedInverted.Response));
    }

    [Fact]
    public void AdjacentOccurrencesPassTheBoundary()
    {
        var validation = Validate(Answer.WithOccurrences(0, 3, 3, Answer.TextCodePoints));

        Assert.Equal(
            [new Occurrence(0, 3, "PERSON", "PROPN", "obl"), new Occurrence(3, Answer.TextCodePoints, "PERSON", "PROPN", "obl")],
            validation.Response!.Occurrences);
    }

    public void Dispose() => recognition.Dispose();

    private static ResponseValidation Validate(byte[] body) =>
        ResponseValidator.Validate(Answer.RequestFor(Answer.Text), ContractVersion.Current, body);

    private static string Body(int start, int end) =>
        Encoding.UTF8.GetString(Answer.WithOccurrence(start, end));
}
