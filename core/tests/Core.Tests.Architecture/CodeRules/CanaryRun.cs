namespace Core.Tests.Architecture.CodeRules;

/// <summary>A canary planted in a production project and built: where each route sits and what the build said.</summary>
internal sealed record CanaryRun(IReadOnlyList<CanarySite> Sites, CommandOutcome Build)
{
    private const string SilencedProject = "Core.Platform";

    public static async Task<CanaryRun> OfProjectAsync(string projectName, CancellationToken cancellationToken)
    {
        using var workspace = CanaryWorkspace.OfProject(SourceTree.ProductionProject(projectName).Path);
        var sites = workspace.PlantCanaries();
        return new CanaryRun(sites, await workspace.BuildAsync(cancellationToken));
    }

    /// <summary>The project built with a fixture that tries to silence the analyzer applied to it.</summary>
    public static async Task<CanaryRun> SilencedByAsync(string fixture, CancellationToken cancellationToken)
    {
        using var workspace = CanaryWorkspace.OfProject(SourceTree.ProductionProject(SilencedProject).Path);
        workspace.Apply(Path.Combine(SourceTree.FixturesDirectory, "logging-canary", fixture));
        var sites = workspace.PlantCanaries();
        return new CanaryRun(sites, await workspace.BuildAsync(cancellationToken));
    }

    public static async Task<CanaryRun> OfFixtureProjectAsync(string fixtureProject, CancellationToken cancellationToken)
    {
        var fixture = Path.Combine(SourceTree.FixturesDirectory, "logging-canary-projects", fixtureProject);
        using var workspace = CanaryWorkspace.OfFixtureProject(fixture, fixtureProject);
        var sites = workspace.PlantCanaries();
        return new CanaryRun(sites, await workspace.BuildAsync(cancellationToken));
    }
}
