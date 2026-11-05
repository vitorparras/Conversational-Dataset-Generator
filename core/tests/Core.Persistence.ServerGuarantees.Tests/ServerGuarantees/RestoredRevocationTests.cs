using System.Text.Json.Nodes;
using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Persistence.Domains;
using Core.Persistence.Tests.Content;
using Core.Persistence.Tests.Fixtures;

namespace Core.Persistence.Tests.ServerGuarantees;

/// <summary>
/// The revocations of the labels in the copy (issue 647): the copy carries them, the load puts them back, and what the restored database encrypts new content under is the newest generation
/// not revoked, not the one the owner had revoked before the copy was made.
/// </summary>
[Trait("Category", "Database")]
public sealed class RestoredRevocationTests
{
    private const string Registering = "INSERT INTO public.derivation_label (label) VALUES ('texto-bruto/1/v1'), ('texto-bruto/1/v2'), ('texto-bruto/1/v3')";
    private const string Revoked = "INSERT INTO public.derivation_label_revocation (label, revoked_at, revoked_by) VALUES ('texto-bruto/1/v3', '2026-03-04 05:06:07+00', 'zqx-owner')";
    private const string Rows = "SELECT label || '|' || revoked_at || '|' || revoked_by FROM public.derivation_label_revocation ORDER BY label";

    private static readonly string[] Generations = ["texto-bruto/1/v1", "texto-bruto/1/v2", "texto-bruto/1/v3"];

    [DatabaseFact]
    public async Task CopyOfADatabaseWithARevokedLabelCarriesTheRevocationAsTheDatabaseHasIt()
    {
        await using var copied = await CopiedWorld.CreateAsync(beforeCopy: [Registering, Revoked]);

        var line = Assert.Single(await File.ReadAllLinesAsync(Path.Combine(copied.Folder, "public.derivation_label_revocation.jsonl"), TestContext.Current.CancellationToken));

        var row = JsonNode.Parse(line)!;
        Assert.Equal(("texto-bruto/1/v3", "zqx-owner"), ((string)row["label"]!, (string)row["revoked_by"]!));
    }

    [DatabaseFact]
    public async Task RestoredDatabaseHoldsTheRevocationAndTheNextWriteUsesTheNewestGenerationNotRevoked()
    {
        var token = TestContext.Current.CancellationToken;
        await using var copied = await CopiedWorld.CreateAsync(beforeCopy: [Registering, Revoked]);
        await using var restored = await RestoredCopy.LoadAsync(copied);
        using var custody = SyntheticCustody.Holding(Generations);

        var revoked = await LabelRevocations.ReadAsync(restored.App, token);
        var registered = await RegisteredLabels.ReadAsync(restored.App, token);

        Assert.Equal(await copied.World.OwnerColumnAsync(Rows), await restored.Database.FirstColumnAsOwnerAsync(Rows));
        Assert.Equal(["texto-bruto/1/v3"], revoked.Order(StringComparer.Ordinal));
        Assert.Equal("texto-bruto/1/v3", LabelOfNewRow(custody, registered, new HashSet<string>(StringComparer.Ordinal)));
        Assert.Equal("texto-bruto/1/v2", LabelOfNewRow(custody, registered, revoked));
    }

    [DatabaseFact]
    public async Task CopyOfADatabaseWithNoRevocationRestoresWithNoneAndTheNewestGenerationWrites()
    {
        var token = TestContext.Current.CancellationToken;
        await using var copied = await CopiedWorld.CreateAsync(beforeCopy: [Registering]);
        await using var restored = await RestoredCopy.LoadAsync(copied);
        using var custody = SyntheticCustody.Holding(Generations);

        var revoked = await LabelRevocations.ReadAsync(restored.App, token);
        var registered = await RegisteredLabels.ReadAsync(restored.App, token);

        Assert.Empty(revoked);
        Assert.Equal(Generations, await restored.Database.FirstColumnAsOwnerAsync("SELECT label FROM public.derivation_label ORDER BY label"));
        Assert.Equal("texto-bruto/1/v3", LabelOfNewRow(custody, registered, revoked));
    }

    private static string LabelOfNewRow(SyntheticCustody custody, IReadOnlySet<string> registered, IReadOnlySet<string> revoked)
    {
        var content = new ProtectedContent(custody.Labels, custody.Keys, registered, revoked);
        using var sealing = content.Begin(ContentColumns.ItemKey, 1);
        return sealing.Label.Text;
    }
}
