using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>Modules meet through contracts and the platform, as docs/arquitetura/002-fronteiras.md describes, with the host as the one composition root.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Lazy<IReadOnlyList<CompilerInvocation>> Production = new(() => ModuleGraph.Invocations(SourceTree.ProductionProjects()));

    private static readonly Lazy<CompilerInvocation> Fixture = new(() => CompilerInvocation.OfFixture("module-boundary", ModuleBoundaryRules.Platform));

    public static TheoryData<string, string> AllowedReferences() => new()
    {
        { "Core.Boundary", "Core.Boundary.Contracts" },
        { "Core.Boundary", "Core.Platform" },
        { "Core.Persistence", "Core.Boundary.Contracts" },
        { "Core.Boundary.Contracts", "Core.Platform" },
        { "Core.Boundary.Contracts", "Core.Persistence.Contracts" },
        { "Core.Host", "Core.Boundary" },
        { "Core.Host", "Core.Boundary.Contracts" },
        { "Core.Host", "Core.Platform" },
    };

    public static TheoryData<string, string> RefusedReferences() => new()
    {
        { "Core.Boundary", "Core.Host" },
        { "Core.Platform", "Core.Boundary" },
        { "Core.Platform", "Core.Boundary.Contracts" },
        { "Core.Platform", "Core.Host" },
        { "Core.Persistence", "Core.Boundary" },
        { "Core.Boundary.Contracts", "Core.Boundary" },
        { "Core.Persistence", "Core.Contracts" },
        { "Core.Persistence", "Core.Boundary.Contracts.Http" },
        { "Core.Persistence", "Core.BoundaryContracts" },
    };

    [Fact]
    public void ProductionModulesReferenceOnlyContractsAndThePlatform()
    {
        var refused = ModuleBoundaryRules.Refused(Production.Value.SelectMany(ModuleGraph.Of));

        Assert.Empty(refused);
    }

    [Fact]
    public void ProductionAssembliesCarryTheirProjectNames()
    {
        var renamed = ModuleBoundaryRules.Renamed(Production.Value);

        Assert.Empty(renamed);
    }

    [Fact]
    public void ProductionGraphIsReadFromTheCompiledReferences()
    {
        var references = Production.Value.SelectMany(ModuleGraph.Of);

        Assert.Contains(new ModuleReference(ModuleBoundaryRules.Host, ModuleBoundaryRules.Platform), references);
    }

    [Theory]
    [MemberData(nameof(AllowedReferences))]
    public void ReferenceToContractsOrThePlatformOrFromTheHostIsAccepted(string from, string to)
    {
        var refused = ModuleBoundaryRules.Refused([new ModuleReference(from, to)]);

        Assert.Empty(refused);
    }

    [Theory]
    [MemberData(nameof(RefusedReferences))]
    public void ReferenceToAModuleOrFromThePlatformIsRefused(string from, string to)
    {
        var reference = new ModuleReference(from, to);

        var refused = ModuleBoundaryRules.Refused([reference]);

        Assert.Equal([reference], refused);
    }

    [Fact]
    public void ReferenceAddedOutsideTheProjectFileIsRefusedUnderTheRealNames()
    {
        var invocation = Fixture.Value;

        var refused = ModuleBoundaryRules.Refused(ModuleGraph.Of(invocation));

        Assert.Equal([new ModuleReference(ModuleBoundaryRules.Platform, "Core.Sample")], refused);
    }

    [Fact]
    public void ProjectWhoseAssemblyTakesTheHostNameIsRefused()
    {
        var invocation = Fixture.Value;

        var renamed = ModuleBoundaryRules.Renamed([invocation]);

        Assert.Equal(["Core.Platform compiles to the assembly Core.Host"], renamed);
    }
}
