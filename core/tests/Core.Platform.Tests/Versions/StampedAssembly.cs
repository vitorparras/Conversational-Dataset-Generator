using System.Reflection;
using System.Reflection.Emit;

namespace Core.Platform.Tests.Versions;

/// <summary>An assembly made in memory, carrying exactly the metadata given, as a build would have stamped it.</summary>
internal static class StampedAssembly
{
    private static readonly ConstructorInfo MetadataConstructor =
        typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)])!;

    public static Assembly With(IReadOnlyDictionary<string, string> stamp) =>
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"ZqxStamped{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect,
            [.. stamp.Select(pair => new CustomAttributeBuilder(MetadataConstructor, [pair.Key, pair.Value]))]);
}
