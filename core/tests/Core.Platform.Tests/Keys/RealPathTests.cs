using Core.Platform.Keys;
using Core.Tests.Support.Custody;

namespace Core.Platform.Tests.Keys;

/// <summary>A path with no link in it comes back as it is, and one with links, in the folders or in the file, comes back as where it really lies.</summary>
public sealed class RealPathTests : IDisposable
{
    private readonly CustodyFiles files = new();

    public void Dispose() => files.Dispose();

    [Fact]
    public void PathWithoutLinksComesBackAsTheFullPath()
    {
        var path = files.WriteText("plain", "x");

        Assert.Equal(Path.GetFullPath(path), RealPath.Of(path));
    }

    [Fact]
    public void PathThatDoesNotExistComesBackAsTheFullPath()
    {
        var missing = Path.Combine(files.Directory, "ainda-nao", "existe");

        Assert.Equal(Path.GetFullPath(missing), RealPath.Of(missing));
    }

    [Fact]
    public void FolderOfTheRootComesBackAsTheRoot()
    {
        var root = Path.GetPathRoot(files.Directory)!;

        Assert.Equal(root, RealPath.Of(root));
    }

    [Fact]
    public void LinkedFolderInTheMiddleOfThePathIsFollowed()
    {
        using var target = new CustodyFiles();
        var inside = target.WriteText("key", "x");
        var link = Path.Combine(files.Directory, "current");
        Assert.SkipUnless(TryFolderLink(link, target.Directory), "a plataforma não deixou criar o elo de pasta");

        Assert.Equal(RealPath.Of(inside), RealPath.Of(Path.Combine(link, "key")));
        Assert.StartsWith(RealPath.Of(target.Directory), RealPath.Of(Path.Combine(link, "key")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LinkedFileIsFollowedToItsTarget()
    {
        using var target = new CustodyFiles();
        var inside = target.WriteText("key", "x");
        var link = Path.Combine(files.Directory, "elo");
        Assert.SkipUnless(TryFileLink(link, inside), "a plataforma não deixou criar o elo simbólico");

        Assert.Equal(RealPath.Of(inside), RealPath.Of(link));
    }

    [Fact]
    public void ChainOfLinksIsFollowedToTheEnd()
    {
        using var target = new CustodyFiles();
        var inside = target.WriteText("key", "x");
        var first = Path.Combine(files.Directory, "um");
        var second = Path.Combine(files.Directory, "dois");
        Assert.SkipUnless(TryFileLink(first, inside) && TryFileLink(second, first), "a plataforma não deixou criar o elo simbólico");

        Assert.Equal(RealPath.Of(inside), RealPath.Of(second));
    }

    [Fact]
    public void LoopOfLinksFailsAsAnIoError()
    {
        var one = Path.Combine(files.Directory, "um");
        var two = Path.Combine(files.Directory, "dois");
        Assert.SkipUnless(TryFileLink(one, two) && TryFileLink(two, one), "a plataforma não deixou criar o elo simbólico");

        _ = Assert.Throws<IOException>(() => RealPath.Of(one));
    }

    private static bool TryFolderLink(string link, string target)
    {
        try
        {
            _ = Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryFileLink(string link, string target)
    {
        try
        {
            _ = File.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
