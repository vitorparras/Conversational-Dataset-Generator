using Core.Tests.Shared;

namespace Core.Tests.Architecture;

/// <summary>Issue 24 CA-3 base: every source project is visible to the architecture tests.</summary>
public sealed class SourceProjectTests
{
    [Fact]
    public void EverySourceProjectProducesAnAssemblyTheTestsCanInspect()
    {
        var sourceProjects = Directory
            .GetDirectories(Path.Combine(RepositoryRoot.Find().FullName, "core", "src"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.NotEmpty(sourceProjects);
        Assert.All(
            sourceProjects,
            name => Assert.True(
                File.Exists(Path.Combine(AppContext.BaseDirectory, name + ".dll")),
                $"{name}.dll is not in the architecture test output; reference it from Core.Host."));
    }
}
