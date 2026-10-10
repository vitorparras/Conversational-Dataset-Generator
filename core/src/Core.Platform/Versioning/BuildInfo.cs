using System.Globalization;
using System.Text.RegularExpressions;

namespace Core.Platform.Versioning;

/// <summary>What the build recorded about itself: version, commit, and whether the tree was dirty.</summary>
public static partial class BuildInfo
{
    /// <summary>The core version.</summary>
    public static VersionValue Version { get; } = VersionValue.OfOrAbsent(GeneratedBuildInfo.Version);

    /// <summary>The commit the artifact was built from; absent when git was not reachable.</summary>
    public static VersionValue Commit { get; } =
        CommitFrom(GeneratedBuildInfo.CommitExitCode, GeneratedBuildInfo.CommitOutput);

    /// <summary>
    /// <c>true</c> when the working tree had changes not in the commit, <c>false</c> when clean,
    /// absent when git was not reachable.
    /// </summary>
    public static VersionValue DirtyTree { get; } =
        DirtyTreeFrom(GeneratedBuildInfo.StatusExitCode, GeneratedBuildInfo.StatusOutputLength);

    /// <summary>
    /// Reads what <c>git rev-parse HEAD</c> answered: the commit when git succeeded and printed a
    /// full hash; absent when git is missing, failed, or printed anything else.
    /// </summary>
    internal static VersionValue CommitFrom(string exitCode, string output) =>
        Succeeded(exitCode) && CommitHash().IsMatch(output) ? VersionValue.Of(output) : Absent.Instance;

    /// <summary>
    /// Reads what <c>git status --porcelain</c> answered: no output is a clean tree (<c>false</c>),
    /// any output a dirty one (<c>true</c>); git missing or failing is absent.
    /// </summary>
    internal static VersionValue DirtyTreeFrom(string exitCode, string outputLength)
    {
        if (!Succeeded(exitCode)
            || !int.TryParse(outputLength, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
        {
            return Absent.Instance;
        }

        return VersionValue.Of(length == 0 ? "false" : "true");
    }

    private static bool Succeeded(string exitCode) => exitCode == "0";

    // SHA-1 or SHA-256, as git prints them.
    [GeneratedRegex("^[0-9a-f]{40}([0-9a-f]{24})?$")]
    private static partial Regex CommitHash();
}
