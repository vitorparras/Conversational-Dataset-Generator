using System.Collections.Concurrent;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>A canary planted in a project and built: where each route sits and what the build said.</summary>
internal sealed record CanaryRun(IReadOnlyList<CanarySite> Sites, CommandOutcome Build, IReadOnlyList<CanarySite> SonarSites)
{
    private const string SilencedProject = "Core.Platform";

    private static readonly ConcurrentDictionary<string, Lazy<Task<CanaryRun>>> Projects = new(StringComparer.Ordinal);

    /// <summary>Both canaries in one build of the project, shared by every test that asks for it.</summary>
    public static Task<CanaryRun> OfProjectAsync(string projectName, CancellationToken cancellationToken) =>
        Projects.GetOrAdd(projectName, name => new Lazy<Task<CanaryRun>>(() => BuildProjectAsync(name, cancellationToken))).Value;

    /// <summary>The project built with a fixture that tries to silence the analyzer applied to it.</summary>
    public static async Task<CanaryRun> SilencedByAsync(string fixture, CancellationToken cancellationToken)
    {
        using var workspace = CanaryWorkspace.OfProject(SourceTree.ProductionProject(SilencedProject).Path);
        workspace.Apply(Path.Combine(SourceTree.FixturesDirectory, "logging-canary", fixture));
        var sites = workspace.PlantCanaries();
        return new CanaryRun(sites, await workspace.BuildAsync(cancellationToken), []);
    }

    public static async Task<CanaryRun> OfFixtureProjectAsync(string fixtureProject, CancellationToken cancellationToken)
    {
        var fixture = Path.Combine(SourceTree.FixturesDirectory, "logging-canary-projects", fixtureProject);
        using var workspace = CanaryWorkspace.OfFixtureProject(fixture, fixtureProject);
        var sites = workspace.PlantCanaries();
        return new CanaryRun(sites, await workspace.BuildAsync(cancellationToken), []);
    }

    /// <summary>A fixture project whose sources try to look generated, built with the Sonar canary only.</summary>
    public static async Task<CanaryRun> OfSonarFixtureAsync(string fixtureProject, CancellationToken cancellationToken)
    {
        var fixture = Path.Combine(SourceTree.FixturesDirectory, "sonar-canary-projects", fixtureProject);
        using var workspace = CanaryWorkspace.OfFixtureProject(fixture, fixtureProject);
        var sites = workspace.PlantSonarCanaries();
        return new CanaryRun([], await workspace.BuildAsync(cancellationToken), sites);
    }

    private static async Task<CanaryRun> BuildProjectAsync(string projectName, CancellationToken cancellationToken)
    {
        using var workspace = CanaryWorkspace.OfProject(SourceTree.ProductionProject(projectName).Path);
        var sites = workspace.PlantCanaries();
        var sonarSites = workspace.PlantSonarCanaries();
        return new CanaryRun(sites, await workspace.BuildAsync(cancellationToken), sonarSites);
    }
}
