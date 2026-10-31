using System.Security.Cryptography;
using Core.Persistence.Migrations;

namespace Core.Persistence.Tests.Migrations;

/// <summary>The steps the build carries are the resources under Sql, in numeric order, each with the digest of its own bytes.</summary>
public sealed class EmbeddedMigrationsTests
{
    [Fact]
    public void FirstStepCreatesTheRegistryAndTheStepsAreNumberedFromOneWithoutGaps()
    {
        var steps = EmbeddedMigrations.Load();

        Assert.Equal("0001-schema-version", steps[0].Name);
        Assert.Equal(Enumerable.Range(1, steps.Count), steps.Select(step => step.Version));
    }

    [Fact]
    public void DigestIsTheSha256OfTheEmbeddedBytesInLowercaseHexadecimal()
    {
        var step = EmbeddedMigrations.Load()[0];
        using var resource = typeof(EmbeddedMigrations).Assembly.GetManifestResourceStream("Sql/0001-schema-version.sql")!;

        var expected = Convert.ToHexStringLower(SHA256.HashData(resource));

        Assert.Equal(expected, step.Sha256);
    }

    [Fact]
    public void EveryStepIsEmbeddedWithLineFeedsOnlySoACheckoutOnAnySystemGivesTheSameDigest()
    {
        var withCarriageReturn = EmbeddedMigrations.Load().Where(step => step.Sql.Contains('\r', StringComparison.Ordinal));

        Assert.Empty(withCarriageReturn);
    }

    [Fact]
    public void SameBytesGiveTheSameDigestAndOneByteMoreGivesAnother()
    {
        var one = MigrationStep.Of(1, "0001-x", "SELECT 1;"u8.ToArray());
        var same = MigrationStep.Of(1, "0001-x", "SELECT 1;"u8.ToArray());
        var other = MigrationStep.Of(1, "0001-x", "SELECT 1; "u8.ToArray());

        Assert.Equal(one.Sha256, same.Sha256);
        Assert.NotEqual(one.Sha256, other.Sha256);
    }
}
