using System.Text;
using Core.Persistence.Migrations;

namespace Core.Persistence.Tests.Migrations;

/// <summary>The registry is set against the steps one place at a time: behind, ahead and divergent are told apart, and an absent registry is version 0.</summary>
public sealed class SchemaComparisonTests
{
    private static readonly MigrationStep First = Step(1, "0001-first", "a");
    private static readonly MigrationStep Second = Step(2, "0002-second", "b");

    [Fact]
    public void EmptyRegistryIsBehindAtVersionZeroAndEveryStepIsPending()
    {
        var comparison = SchemaComparison.Compare([First, Second], []);

        Assert.Equal(SchemaState.Behind, comparison.State);
        Assert.Equal(0, comparison.Found);
        Assert.Equal(2, comparison.Expected);
        Assert.Equal(0, comparison.Applied);
    }

    [Fact]
    public void RegistryHoldingEveryStepUnchangedIsUpToDate()
    {
        var comparison = SchemaComparison.Compare([First, Second], [Applied(First), Applied(Second)]);

        Assert.Equal(SchemaState.UpToDate, comparison.State);
        Assert.Equal(2, comparison.Found);
    }

    [Fact]
    public void RegistryHoldingALeadingPartIsBehindAndKeepsWhereThePendingBegin()
    {
        var comparison = SchemaComparison.Compare([First, Second], [Applied(First)]);

        Assert.Equal(SchemaState.Behind, comparison.State);
        Assert.Equal(1, comparison.Found);
        Assert.Equal(1, comparison.Applied);
    }

    [Fact]
    public void RegistryHoldingAStepThisBuildDoesNotCarryIsAhead()
    {
        var comparison = SchemaComparison.Compare([First], [Applied(First), Applied(Second)]);

        Assert.Equal(SchemaState.Ahead, comparison.State);
        Assert.Equal(2, comparison.Found);
        Assert.Equal(1, comparison.Expected);
    }

    [Fact]
    public void StepWithAnotherDigestIsDivergentNamingTheStepAndBothDigests()
    {
        var changed = Step(1, "0001-first", "changed");

        var comparison = SchemaComparison.Compare([changed, Second], [Applied(First)]);

        Assert.Equal(SchemaState.Divergent, comparison.State);
        Assert.Equal("0001-first", comparison.Step);
        Assert.Equal(First.Sha256, comparison.Registered);
        Assert.Equal(changed.Sha256, comparison.Current);
    }

    [Fact]
    public void RegistryWithAGapInTheVersionsIsDivergent()
    {
        var comparison = SchemaComparison.Compare([First, Second], [Applied(Second)]);

        Assert.Equal(SchemaState.Divergent, comparison.State);
        Assert.Equal("0001-first", comparison.Step);
    }

    [Fact]
    public void BuildWithoutStepsAndRegistryWithoutRowsIsUpToDateAtVersionZero()
    {
        var comparison = SchemaComparison.Compare([], []);

        Assert.Equal(SchemaState.UpToDate, comparison.State);
        Assert.Equal(0, comparison.Found);
        Assert.Equal(0, comparison.Expected);
        Assert.Equal(0, comparison.Applied);
    }

    [Fact]
    public void RegistryHoldingAStepWhenTheBuildCarriesNoneIsAheadExpectingVersionZero()
    {
        var comparison = SchemaComparison.Compare([], [Applied(First)]);

        Assert.Equal(SchemaState.Ahead, comparison.State);
        Assert.Equal(1, comparison.Found);
        Assert.Equal(0, comparison.Expected);
    }

    private static MigrationStep Step(int version, string name, string sql) =>
        MigrationStep.Of(version, name, Encoding.UTF8.GetBytes(sql));

    private static AppliedStep Applied(MigrationStep step) => new(step.Version, step.Name, step.Sha256);
}
