using Core.Persistence.Migrations;

namespace Core.Persistence.Tests.Migrations;

/// <summary>A step is its version, its name and its text, with the lowercase SHA-256 of the bytes it came from; a step without a name or text cannot be built.</summary>
public sealed class MigrationStepTests
{
    [Fact]
    public void StepKeepsItsVersionNameAndText()
    {
        var step = MigrationStep.Of(7, "0007-x", "SELECT 1;"u8.ToArray());

        Assert.Equal(7, step.Version);
        Assert.Equal("0007-x", step.Name);
        Assert.Equal("SELECT 1;", step.Sql);
    }

    [Fact]
    public void DigestIsTheKnownSha256OfTheBytesInLowercaseHexadecimal()
    {
        var step = MigrationStep.Of(1, "0001-x", "a"u8.ToArray());

        Assert.Equal("ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb", step.Sha256);
    }

    [Fact]
    public void StepWithoutANameIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => MigrationStep.Of(1, null!, "a"u8.ToArray()));

        Assert.Equal("name", defect.ParamName);
    }

    [Fact]
    public void StepWithoutContentIsADefect()
    {
        var defect = Assert.Throws<ArgumentNullException>(() => MigrationStep.Of(1, "0001-x", null!));

        Assert.Equal("content", defect.ParamName);
    }
}
