using Core.Persistence.Content;
using Core.Persistence.Content.Sealing;
using Core.Platform.Keys;

namespace Core.Persistence.Tests.Content;

/// <summary>
/// The write follows the rotation (issue 56, CA-1): new content is sealed under the newest generation of the domain that the database registered and the custody file holds, not under the newest
/// the file holds, so a generation put in the file before the rotation seals nothing until the rotation registers it, and what was sealed under the older one opens all the same.
/// </summary>
public sealed class ProtectedContentRotationTests
{
    private const string Text = "texto sintetico do item";

    [Fact]
    public void ALabelInTheFileThatTheRotationDidNotRegisterSealsNothingYet()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2");
        var content = custody.ContentRegistering(["texto-bruto/7/v1"]);

        Assert.Equal("texto-bruto/7/v1", LabelOfNewRow(content, 7));
    }

    [Fact]
    public void OnceTheRotationRegistersTheGenerationTheNewRowsAreSealedUnderItAndTheOldRowOpens()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2");
        using var before = custody.ContentRegistering(["texto-bruto/7/v1"]).Begin(ContentColumns.ItemKey, 7);
        var old = before.Seal(ContentColumns.RawText, Text);
        var content = custody.ContentRegistering(["texto-bruto/7/v1", "texto-bruto/7/v2"]);

        var opened = content.Open(ContentColumns.RawText, 7, old.Envelope.Span, before.Stored);

        Assert.Equal((Text, "texto-bruto/7/v2"), (opened, LabelOfNewRow(content, 7)));
    }

    [Fact]
    public void TheRotationOfOneDomainLeavesTheOtherSealingUnderItsOwnGeneration()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1", "texto-bruto/7/v2", "texto-bruto/8/v1");
        var content = custody.ContentRegistering(["texto-bruto/7/v1", "texto-bruto/7/v2", "texto-bruto/8/v1"]);

        Assert.Equal(("texto-bruto/7/v2", "texto-bruto/8/v1"), (LabelOfNewRow(content, 7), LabelOfNewRow(content, 8)));
    }

    [Fact]
    public void ARegisteredLabelThatTheFileDoesNotHoldSealsNothing()
    {
        using var custody = SyntheticCustody.Holding("texto-bruto/7/v1");
        var content = custody.ContentRegistering(["texto-bruto/7/v1", "texto-bruto/7/v2"]);

        Assert.Equal("texto-bruto/7/v1", LabelOfNewRow(content, 7));
    }

    private static string LabelOfNewRow(ProtectedContent content, int domainId)
    {
        using var sealing = content.Begin(ContentColumns.ItemKey, domainId);
        return EnvelopeCipher.LabelOf(sealing.Seal(ContentColumns.RawText, Text).Envelope.ToArray());
    }
}
