using DotNet.Testcontainers.Configurations;

namespace Core.Tests.Integration.Fixtures;

/// <summary>Decides whether the database tests run: they skip, with a visible reason, only where Docker does not answer and CI does not demand them.</summary>
internal static class DockerAvailability
{
    public const string SkipReason = "Docker indisponível: teste de banco saltado";

    private static readonly Lazy<bool> Run = new(() => Decide(TestcontainersSettings.OS.DockerEndpointAuthConfig is not null, Environment.GetEnvironmentVariable("CI")));

    /// <summary>Read by <see cref="DatabaseFactAttribute"/> through its name.</summary>
    public static bool ShouldRun => Run.Value;

    /// <summary>In CI a missing daemon is not a reason to skip: the test runs and fails on the missing daemon.</summary>
    public static bool Decide(bool daemonResponds, string? continuousIntegration) =>
        daemonResponds || string.Equals(continuousIntegration, "true", StringComparison.OrdinalIgnoreCase);
}
