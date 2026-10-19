using Core.Tests.Architecture.CodeRules;

namespace Core.Tests.Architecture.Boundaries;

/// <summary>A module reaches another only through its contracts or the platform, the platform reaches no module, and the host, which composes them all, reaches any.</summary>
internal static class ModuleBoundaryRules
{
    public const string Host = "Core.Host";
    public const string Platform = "Core.Platform";
    private const string Prefix = "Core";
    private const string ContractsSuffix = "Contracts";

    /// <summary>Every reference the boundaries do not allow.</summary>
    public static IEnumerable<ModuleReference> Refused(IEnumerable<ModuleReference> references) =>
        references.Where(reference => !IsAllowed(reference)).Distinct();

    /// <summary>Every project whose assembly goes by another name, which would let it pass for a different module.</summary>
    public static IEnumerable<string> Renamed(IEnumerable<CompilerInvocation> invocations) =>
        invocations
            .Where(invocation => invocation.AssemblyName != ModuleGraph.ProjectName(invocation))
            .Select(invocation => $"{ModuleGraph.ProjectName(invocation)} compiles to the assembly {invocation.AssemblyName}");

    private static bool IsAllowed(ModuleReference reference) => reference.From switch
    {
        Host => true,
        Platform => false,
        _ => reference.To == Platform || IsContracts(reference.To),
    };

    /// <summary>A name of the form <c>Core.Y.Contracts</c>: the contracts of some module, never a module itself.</summary>
    private static bool IsContracts(string module)
    {
        var segments = module.Split('.');
        return segments.Length >= 3 && segments[0] == Prefix && segments[^1] == ContractsSuffix;
    }
}
