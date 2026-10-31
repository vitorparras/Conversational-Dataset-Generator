using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Core.Host.Tests;

/// <summary>
/// The data source of the host does not log the values of the parameters (#69, CA-4): a salt or a result sent as a parameter would reach the log with them, and the tests of the records
/// that capture the driver's log would not notice, since the driver writes a byte array as its type name. The test reads the calls each assembly of the core makes, not its source text,
/// and refuses both ways of switching it on.
/// </summary>
public sealed class ParameterLoggingTests
{
    private static readonly string[] Enablers = ["EnableParameterLogging", "InitializeLogging"];

    [Fact]
    public void NoAssemblyOfTheCoreEnablesTheLoggingOfParameters()
    {
        var assemblies = CoreAssemblies();

        Assert.Contains(assemblies, path => Path.GetFileName(path) == "Core.Host.dll");
        Assert.Contains(assemblies, path => Path.GetFileName(path) == "Core.Persistence.dll");
        Assert.All(assemblies, path => Assert.Empty(Enablers.Intersect(MembersCalledBy(path), StringComparer.Ordinal)));
    }

    [Fact]
    public void ReadingTheCallsOfAnAssemblyFindsTheOnesItMakes()
    {
        var persistence = Path.Combine(AppContext.BaseDirectory, "Core.Persistence.dll");

        Assert.Contains("CreateCommand", MembersCalledBy(persistence));
    }

    private static string[] CoreAssemblies() =>
        [.. Directory.EnumerateFiles(AppContext.BaseDirectory, "Core.*.dll").Where(path => !Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))];

    private static HashSet<string> MembersCalledBy(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        return [.. reader.MemberReferences.Select(handle => reader.GetString(reader.GetMemberReference(handle).Name))];
    }
}
