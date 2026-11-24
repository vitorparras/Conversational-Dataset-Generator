using System.Net;
using Core.Host.Endpoints;
using Core.Host.Tests.Submissions;
using Core.Ingestion.Contracts.Dating;
using Core.Persistence.Content.Dating;
using Core.Persistence.Tests.Fixtures;

namespace Core.Host.Tests.Dating;

/// <summary>
/// The shift of the dates of a domain is told to nobody (issue 108, CA-4): with the shift of the domain established by the store of production in the database the host serves,
/// no screen the host answers about the domain and no line it writes to its log shows it, in any form a shift is written in.
/// </summary>
[Trait("Category", "Database")]
public sealed class DateShiftDisclosureTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task ShiftValueAppearsInNoLogScreenOrResponse()
    {
        using var log = new RecordedLog();
        await using var world = await SampleReviewWorld.StartAsync(size: 2, messages: 4, log);
        await world.DrawAndReviewAsync(reviewed: 1);
        var shift = await EstablishAsync(world);
        string[] screens = [ScreenPaths.Batches, ScreenPaths.BatchOf(world.Batch), world.ReviewRoute, ScreenPaths.BatchFindingOf(world.Batch, 2)];

        var replies = new List<Reply>();
        foreach (var screen in screens)
        {
            replies.Add(await world.GetAsync(screen, world.Curator));
        }

        Assert.All(replies, reply => Assert.Equal(HttpStatusCode.OK, reply.Status));
        string[] written =
        [
            .. replies.Select(reply => reply.Page), .. replies.Select(reply => reply.Location ?? string.Empty),
            .. log.Lines, .. world.Submissions.Log.Lines.Select(entry => entry.Line),
        ];
        Assert.Empty(ShiftSweep.Showing(written, shift));
    }

    [Fact]
    public void SweepFindsTheShiftInEveryFormWrittenToALogOrAPage()
    {
        var shift = DateShift.Read("-1234")!;
        var forms = ShiftSweep.Forms(shift);
        string[] written = [.. forms.Select(form => $"Information: zqx deslocamento {form}"), .. forms.Select(form => $"<td>{form}</td>")];

        var found = ShiftSweep.Showing(written, shift);

        Assert.Equal(forms.Count * 2, found.Count);
    }

    [Fact]
    public void SweepTakesNoOtherNumberNorTheDigitsInsideAnotherForAShift()
    {
        var shift = DateShift.Read("1234")!;
        string[] others = ["lote 11234 dias", "21234.00:00:00", "1234567 itens", "12:34", "1.1234 dias", "id 1235 dias", "P11234D", "-1234 dias"];

        var found = ShiftSweep.Showing(others, shift);

        Assert.Empty(found);
    }

    private static async Task<DateShift> EstablishAsync(SampleReviewWorld world)
    {
        var content = SubmissionWorld.SealingOf(["texto-bruto/1/v1", "deslocamento-datas/1/v1"]);
        var reading = await new DateShifts(world.Submissions.DataSource, content).EstablishAsync(SubmissionWorld.Domain, Token);
        Assert.Equal(DateShiftOutcome.Kept, reading.Outcome);
        return reading.Shift!;
    }
}
