using System.Reflection;
using Core.Tests.Shared;

namespace Core.Tests.Architecture;

/// <summary>
/// The architecture rules, by reflection over compiled assemblies, without a third-party library.
/// Each rule returns its violations, so a test can show them all and a negative proof can count them.
/// </summary>
internal static class ArchitectureRules
{
    public const string Host = "Core.Host";
    public const string Platform = "Core.Platform";

    /// <summary>
    /// Terms of the domain that no composition or platform code may name (issue 24 CA-3): annotation
    /// categories, forms of material, pipeline steps. Matched as substrings, ignoring case.
    /// </summary>
    public static IReadOnlyList<string> DomainTerms { get; } =
    [
        "annotation", "category", "taxonomy", "conversation", "document", "comment",
        "sanitiz", "pseudonym", "ingest", "export", "custody",
    ];

    /// <summary>Every assembly built from a project folder under <c>core/src</c>; a new module is covered by convention.</summary>
    public static IReadOnlyList<Assembly> ProductionAssemblies() =>
        [.. Directory.GetDirectories(Path.Combine(RepositoryRoot.Find().FullName, "core", "src"))
            .Select(directory => Assembly.Load(new AssemblyName(Path.GetFileName(directory))))];

    /// <summary>
    /// A module reaches another module only through its <c>.Contracts</c>, and everyone may use
    /// <c>Core.Platform</c>. The host is the composition root and references implementations;
    /// the platform and the contracts reference no module at all.
    /// </summary>
    public static IReadOnlyList<string> ModuleBoundaryViolations(Assembly assembly)
    {
        var name = assembly.GetName().Name!;
        if (name == Host)
        {
            return [];
        }

        var isBase = name == Platform || name.EndsWith(".Contracts", StringComparison.Ordinal);
        return [.. assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(reference => reference.StartsWith("Core.", StringComparison.Ordinal))
            .Where(reference => isBase
                ? reference != Platform
                : reference != Platform && !reference.EndsWith(".Contracts", StringComparison.Ordinal))
            .Select(reference => $"{name} references {reference}")];
    }

    /// <summary>
    /// Every identifier declared in the assembly, public or internal: namespaces, types, members,
    /// and the parameters of methods the assembly itself defines (not those it must name after an
    /// override or an interface it implements).
    /// </summary>
    public static IReadOnlyList<string> DomainTermViolations(Assembly assembly) =>
        [.. DeclaredIdentifiers(assembly)
            .Distinct(StringComparer.Ordinal)
            .SelectMany(identifier => DomainTerms
                .Where(term => identifier.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(term => $"{assembly.GetName().Name}: '{identifier}' contains '{term}'"))];

    private static IEnumerable<string> DeclaredIdentifiers(Assembly assembly)
    {
        const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in assembly.GetTypes())
        {
            if (type.Namespace is { } ns)
            {
                yield return ns;
            }

            yield return type.Name;
            var external = ExternallyNamedMethods(type);
            foreach (var member in type.GetMembers(Declared))
            {
                yield return member.Name;
                if (member is MethodBase method && !external.Contains(method))
                {
                    foreach (var parameter in method.GetParameters())
                    {
                        if (parameter.Name is { } parameterName)
                        {
                            yield return parameterName;
                        }
                    }
                }
            }
        }
    }

    // Overrides and interface implementations take their parameter names from the declaration they
    // implement (CA1725), which belongs to someone else.
    private static HashSet<MethodBase> ExternallyNamedMethods(Type type)
    {
        var methods = new HashSet<MethodBase>();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (method.GetBaseDefinition().DeclaringType != type)
            {
                _ = methods.Add(method);
            }
        }

        if (!type.IsInterface)
        {
            foreach (var contract in type.GetInterfaces())
            {
                foreach (var method in type.GetInterfaceMap(contract).TargetMethods)
                {
                    _ = methods.Add(method);
                }
            }
        }

        return methods;
    }
}
