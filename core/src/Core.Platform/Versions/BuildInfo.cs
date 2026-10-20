using System.Reflection;

namespace Core.Platform.Versions;

/// <summary>Where an executable came from, as its build stamped it; whatever the build could not obtain reads as absent.</summary>
/// <param name="Version">The package version of the build.</param>
/// <param name="Commit">The commit the build was made from.</param>
/// <param name="WorkingTree">Whether the working tree had changes not yet committed.</param>
public sealed record BuildInfo(VersionValue Version, VersionValue Commit, WorkingTree WorkingTree)
{
    private const string VersionKey = "BuildInfo.Version";
    private const string CommitKey = "BuildInfo.Commit";
    private const string WorkingTreeKey = "BuildInfo.WorkingTree";
    private const string RuntimeIdentifierKey = "BuildInfo.RuntimeIdentifier";

    /// <summary>The target the executable was published for; absent when it was built for no target.</summary>
    public VersionValue RuntimeIdentifier { get; private init; } = VersionValue.Absent;

    /// <summary>Reads the stamp the build left on the assembly; a key missing or stamped empty reads as absent, never as empty text.</summary>
    public static BuildInfo Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var stamp = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToList();
        return new BuildInfo(
            VersionOf(Stamped(stamp, VersionKey)), VersionOf(Stamped(stamp, CommitKey)), TreeOf(Stamped(stamp, WorkingTreeKey)))
        {
            RuntimeIdentifier = VersionOf(Stamped(stamp, RuntimeIdentifierKey)),
        };
    }

    private static string? Stamped(List<AssemblyMetadataAttribute> stamp, string key) =>
        stamp.Find(attribute => attribute.Key == key)?.Value;

    private static VersionValue VersionOf(string? text) =>
        string.IsNullOrWhiteSpace(text) ? VersionValue.Absent : VersionValue.Present(text);

    private static WorkingTree TreeOf(string? text) => text switch
    {
        nameof(WorkingTree.Clean) => WorkingTree.Clean,
        nameof(WorkingTree.Dirty) => WorkingTree.Dirty,
        _ => WorkingTree.Unknown,
    };
}
