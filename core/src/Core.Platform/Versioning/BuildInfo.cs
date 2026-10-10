namespace Core.Platform.Versioning;

/// <summary>What the build recorded about itself: version, commit, and whether the tree was dirty.</summary>
public static class BuildInfo
{
    /// <summary>The core version.</summary>
    public static VersionValue Version { get; } = VersionValue.OfOrAbsent(GeneratedBuildInfo.Version);

    /// <summary>The commit the artifact was built from; absent when git was not reachable.</summary>
    public static VersionValue Commit { get; } = VersionValue.OfOrAbsent(GeneratedBuildInfo.Commit);

    /// <summary>
    /// <c>true</c> when the working tree had changes not in the commit, <c>false</c> when clean,
    /// absent when git was not reachable.
    /// </summary>
    public static VersionValue DirtyTree { get; } = GeneratedBuildInfo.Tree switch
    {
        "dirty" => VersionValue.Of("true"),
        "clean" => VersionValue.Of("false"),
        _ => Absent.Instance,
    };
}
