using Core.Platform.Versions;

namespace Core.Platform.Tests.Versions;

/// <summary>The build stamp is read key by key; a key the assembly does not carry, or carries empty, reads as absent.</summary>
public sealed class BuildInfoTests
{
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";

    private static readonly Dictionary<string, string> FullStamp = new(StringComparer.Ordinal)
    {
        ["BuildInfo.Version"] = "9.8.7",
        ["BuildInfo.Commit"] = Commit,
        ["BuildInfo.WorkingTree"] = "Clean",
    };

    [Fact]
    public void StampedVersionIsReadUnderItsOwnKey()
    {
        var build = BuildInfo.Of(StampedAssembly.With(FullStamp));

        Assert.Equal(VersionValue.Present("9.8.7"), build.Version);
    }

    [Fact]
    public void StampedCommitIsReadUnderItsOwnKey()
    {
        var build = BuildInfo.Of(StampedAssembly.With(FullStamp));

        Assert.Equal(VersionValue.Present(Commit), build.Commit);
    }

    [Fact]
    public void CleanStampReadsAsCleanTree()
    {
        var build = BuildInfo.Of(StampedAssembly.With(FullStamp));

        Assert.Equal(WorkingTree.Clean, build.WorkingTree);
    }

    [Fact]
    public void DirtyStampReadsAsDirtyTree()
    {
        var build = BuildInfo.Of(StampedAssembly.With(Only("BuildInfo.WorkingTree", "Dirty")));

        Assert.Equal(WorkingTree.Dirty, build.WorkingTree);
    }

    [Fact]
    public void UnstampedAssemblyReadsEveryValueAbsent()
    {
        var build = BuildInfo.Of(StampedAssembly.With(new Dictionary<string, string>(StringComparer.Ordinal)));

        Assert.Equal(new BuildInfo(VersionValue.Absent, VersionValue.Absent, WorkingTree.Unknown), build);
    }

    [Fact]
    public void CommitStampedEmptyReadsAsAbsentAndNotAsEmptyText()
    {
        var build = BuildInfo.Of(StampedAssembly.With(Only("BuildInfo.Commit", string.Empty)));

        Assert.Equal(VersionValue.Absent, build.Commit);
    }

    [Fact]
    public void TreeStampOutsideCleanAndDirtyReadsAsUnknown()
    {
        var build = BuildInfo.Of(StampedAssembly.With(Only("BuildInfo.WorkingTree", "clean")));

        Assert.Equal(WorkingTree.Unknown, build.WorkingTree);
    }

    private static Dictionary<string, string> Only(string key, string value) => new(StringComparer.Ordinal) { [key] = value };
}
