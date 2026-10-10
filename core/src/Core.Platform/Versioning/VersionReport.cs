namespace Core.Platform.Versioning;

/// <summary>
/// The versions the system runs under (issue 51). A version not yet obtainable is
/// <see cref="Absent"/>: the contract until the boundary exists, the model until the language
/// service reports it, the schema until a migration ran.
/// </summary>
/// <param name="Core">The core version.</param>
/// <param name="Commit">The commit the artifact came from.</param>
/// <param name="DirtyTree">Whether the tree had uncommitted changes at build time.</param>
/// <param name="Contract">The version of the boundary contract.</param>
/// <param name="Model">The model version the language service reported.</param>
/// <param name="Schema">The database schema version.</param>
public sealed record VersionReport(
    VersionValue Core,
    VersionValue Commit,
    VersionValue DirtyTree,
    VersionValue Contract,
    VersionValue Model,
    VersionValue Schema)
{
    /// <summary>The report as the build and this stage know it.</summary>
    public static VersionReport FromBuild() =>
        new(BuildInfo.Version, BuildInfo.Commit, BuildInfo.DirtyTree, Absent.Instance, Absent.Instance, Absent.Instance);

    /// <summary>The fields in a fixed order, under the names the status and the log use.</summary>
    public IReadOnlyList<(string Name, VersionValue Value)> Fields =>
    [
        ("core", Core), ("commit", Commit), ("dirtyTree", DirtyTree),
        ("contract", Contract), ("model", Model), ("schema", Schema),
    ];

    /// <summary>One line, <c>name=value</c> per field, the same in the log and in any comparison.</summary>
    public string Describe() => string.Join(" ", Fields.Select(field => $"{field.Name}={field.Value}"));
}
