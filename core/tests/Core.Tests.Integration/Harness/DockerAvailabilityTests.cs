using Core.Tests.Integration.Fixtures;

namespace Core.Tests.Integration.Harness;

/// <summary>A database test never disappears in silence: it skips with a visible reason, and never in CI.</summary>
public sealed class DockerAvailabilityTests
{
    [Fact]
    public void DatabaseFactNamesDockerAsTheReasonToSkip()
    {
        var fact = new DatabaseFactAttribute();

        Assert.Equal("Docker indisponível: teste de banco saltado", fact.Skip);
    }

    [Fact]
    public void DatabaseFactSkipsOnlyWhenDockerAvailabilitySaysSo()
    {
        var fact = new DatabaseFactAttribute();

        Assert.Equal(typeof(DockerAvailability), fact.SkipType);
        Assert.Equal(nameof(DockerAvailability.ShouldRun), fact.SkipUnless);
    }

    [Theory]
    [InlineData(true, null, true)]
    [InlineData(false, null, false)]
    [InlineData(false, "false", false)]
    [InlineData(false, "true", true)]
    public void DatabaseTestsRunWhereDockerAnswersOrCiDemandsThem(bool daemonResponds, string? continuousIntegration, bool expected)
    {
        var run = DockerAvailability.Decide(daemonResponds, continuousIntegration);

        Assert.Equal(expected, run);
    }
}
