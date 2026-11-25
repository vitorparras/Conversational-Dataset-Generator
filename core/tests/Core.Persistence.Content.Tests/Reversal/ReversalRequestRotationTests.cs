using Core.Persistence.Content.Reading;
using Core.Persistence.Content.Reversal;
using Core.Persistence.Content.Rotation;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;
using Core.Platform.Keys;
using static Core.Persistence.Tests.Reversal.ReversalAsks;

namespace Core.Persistence.Tests.Reversal;

/// <summary>
/// The key that wraps the text of a request goes where the keys of the other sealed rows go: the rotation of the epoch and the rewrap of the domain wrap it again, the status of the
/// rewrap counts it, and the text reads as before; nothing else of the request changes.
/// </summary>
[Trait("Category", "Database")]
public sealed class ReversalRequestRotationTests
{
    private const string Register = "INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/1/v1'), ('mapa-reversao/1/v1'), ('texto-bruto/2/v1'), ('mapa-reversao/2/v1'), "
        + "('texto-bruto/1/v2'), ('mapa-reversao/1/v2')";

    private const string Key = "SELECT dek_epoch::text || '|' || encode(dek_wrapped, 'hex') FROM content.reversal_request WHERE id = @id";

    private const string Asked = "SELECT item_id || '|' || marker || '|' || requested_by || '|' || term_days || '|' || encode(justification, 'hex') FROM content.reversal_request WHERE id = @id";

    private static readonly string[] WithTheNextGeneration = [.. ContentWorld.Labels, "texto-bruto/1/v2", "mapa-reversao/1/v2"];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [DatabaseFact]
    public async Task RotationOfTheEpochCarriesTheRequestToTheNewestEpoch()
    {
        await using var world = await ContentWorld.CreateAsync();
        var request = await AskedAsync(world);
        var asked = await world.InDomainAsync(1, Asked, ("id", request));
        using var epochs = SyntheticCustody.HoldingEpochs([1, 2], ContentWorld.Labels);
        using var onlyTheNewest = SyntheticCustody.Holding(2, ContentWorld.Labels);

        _ = await new EpochRotation(world.DataSource, epochs.Keys).RotateAsync(Token);

        var read = await new ReversalRequestReader(world.DataSource, onlyTheNewest.Content, TimeProvider.System).ReadTextAsync(new ReversalAct(1, request, 3), Token);
        Assert.Equal((ContentReadOutcome.Read, "zqx-porque", "zqx-uso"), (read.Outcome, read.Reason, read.Use));
        Assert.Equal(("2", asked), (((string)(await world.InDomainAsync(1, Key, ("id", request)))!).Split('|')[0], await world.InDomainAsync(1, Asked, ("id", request))));
    }

    [DatabaseFact]
    public async Task RewrapOfTheDomainCarriesTheRequestToTheNewestLabelAndItsStatusCountsIt()
    {
        await using var world = await ContentWorld.CreateAsync();
        _ = await world.ScalarAsync(Register);
        var (item, marker) = await ItemAsync(world, 1);
        using var custody = SyntheticCustody.HoldingEpochs([1], WithTheNextGeneration);
        var rewrap = new DomainRewrap(world.DataSource, 500);
        var withoutTheRequest = await rewrap.StatusAsync(1, custody.Labels, Token);
        var request = Assert.NotNull((await world.ReversalRequests.AskAsync(1, 3, Draft(item, marker), Token)).Id);
        var asked = await world.InDomainAsync(1, Asked, ("id", request));

        var withTheRequest = await rewrap.StatusAsync(1, custody.Labels, Token);
        var rewrapped = await rewrap.RewrapAsync(1, custody.Keys, custody.Labels, Token);

        var key = Convert.FromHexString(((string)(await world.InDomainAsync(1, Key, ("id", request)))!).Split('|')[1]);
        var read = await new ReversalRequestReader(world.DataSource, custody.Content, TimeProvider.System).ReadTextAsync(new ReversalAct(1, request, 3), Token);
        Assert.Equal(
            (withoutTheRequest with { Behind = withoutTheRequest.Behind + 1 }, withTheRequest.Behind, "mapa-reversao/1/v2"),
            (withTheRequest, rewrapped, EnvelopeCipher.LabelOf(key)));
        Assert.Equal(
            (new RewrapStatus(withTheRequest.Current + withTheRequest.Behind, 0), ContentReadOutcome.Read, "zqx-porque", asked),
            (await rewrap.StatusAsync(1, custody.Labels, Token), read.Outcome, read.Reason, await world.InDomainAsync(1, Asked, ("id", request))));
    }
}
