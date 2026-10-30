using Core.Platform.Keys;

namespace Core.Platform.Tests.Keys;

/// <summary>The labels of derivation only grow: a set never loses one, and the ones a start finds in use but not provided are named, in order and once.</summary>
public sealed class DerivationLabelsTests
{
    [Fact]
    public void SetHoldsTheLabelsGivenInOrderAndOnceEach()
    {
        var labels = DerivationLabels.Of(["dominio-b", "dominio-a", "dominio-b"]);

        Assert.Equal(["dominio-a", "dominio-b"], labels.All);
    }

    [Fact]
    public void NoneHoldsNoLabel() =>
        Assert.Empty(DerivationLabels.None.All);

    [Fact]
    public void AddingALabelGivesASetWithItAndKeepsTheOnesItHad()
    {
        var before = DerivationLabels.Of(["dominio-a"]);

        var after = before.With("dominio-b");

        Assert.Equal(["dominio-a", "dominio-b"], after.All);
    }

    [Fact]
    public void AddingALabelLeavesTheOriginalSetAsItWas()
    {
        var before = DerivationLabels.Of(["dominio-a"]);

        _ = before.With("dominio-b");

        Assert.Equal(["dominio-a"], before.All);
    }

    [Fact]
    public void AddingALabelAlreadyHeldChangesNothing()
    {
        var after = DerivationLabels.Of(["dominio-a"]).With("dominio-a");

        Assert.Equal(["dominio-a"], after.All);
    }

    [Fact]
    public void LabelsInUseThatTheSetLacksAreNamedInOrderAndOnceEach()
    {
        var provided = DerivationLabels.Of(["dominio-a"]);

        var lacking = provided.Lacking(["dominio-c", "dominio-a", "dominio-b", "dominio-c"]);

        Assert.Equal(["dominio-b", "dominio-c"], lacking);
    }

    [Fact]
    public void LabelsInUseThatTheSetHoldsLackNothing()
    {
        var provided = DerivationLabels.Of(["dominio-a", "dominio-b"]);

        Assert.Empty(provided.Lacking(["dominio-b"]));
    }

    [Fact]
    public void LabelsAreComparedByTheirExactText()
    {
        var provided = DerivationLabels.Of(["dominio-a"]);

        Assert.Equal(["Dominio-A"], provided.Lacking(["Dominio-A"]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" dominio-a")]
    [InlineData("dominio-a ")]
    public void LabelEmptyOrWithSpacesAroundItIsADefect(string label)
    {
        _ = Assert.Throws<ArgumentException>(() => DerivationLabels.Of([label]));
        _ = Assert.Throws<ArgumentException>(() => DerivationLabels.None.With(label));
    }

    [Fact]
    public void NothingInPlaceOfTheLabelsIsADefect()
    {
        _ = Assert.Throws<ArgumentNullException>(() => DerivationLabels.Of(null!));
        _ = Assert.Throws<ArgumentNullException>(() => DerivationLabels.None.Lacking(null!));
    }

    [Fact]
    public void LabelsToCompareThatAreMissingNameTheArgumentThatWasMissing()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => DerivationLabels.None.Lacking(null!));

        Assert.Equal("used", defect.ParamName);
    }

    [Theory]
    [InlineData(" dominio-a")]
    [InlineData("dominio-a ")]
    public void LabelWithSpacesAroundItSaysSoAndNamesTheLabel(string label)
    {
        var defect = Assert.Throws<ArgumentException>(() => DerivationLabels.None.With(label));

        Assert.Equal("label", defect.ParamName);
        Assert.Contains("no space around it", defect.Message, StringComparison.Ordinal);
    }
}
