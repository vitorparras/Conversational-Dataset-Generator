using System.Security.Cryptography;
using System.Text;

namespace Core.Host.Tests.Privacy;

/// <summary>
/// The build tree of the publish: a folder of its own in the temporary folder of the machine, one per working copy, that outlives the test process and is held by one process at a time.
/// The first publish in a machine builds in it from nothing; the next ones, of other test processes, only find what changed. The mutation run starts a test process
/// for the initial run and others to capture the coverage of each test, and a publish that began from nothing in each of them lasted longer than the coverage capture waits for a test.
/// </summary>
internal sealed class PublishTree : IDisposable
{
    private static readonly TimeSpan Retry = TimeSpan.FromMilliseconds(500);
    private readonly FileStream held;

    private PublishTree(string path, FileStream held)
    {
        Path = path;
        this.held = held;
    }

    /// <summary>The folder to give to `--artifacts-path`.</summary>
    public string Path { get; }

    /// <summary>The folder of the working copy at <paramref name="root"/>, waiting for whoever holds it to let it go.</summary>
    public static async Task<PublishTree> TakeAsync(string root, CancellationToken cancellationToken)
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "core-host-publish", FolderOf(root));
        _ = Directory.CreateDirectory(folder);
        var lockFile = System.IO.Path.Combine(folder, "tree.lock");
        while (true)
        {
            try
            {
                return new PublishTree(
                    System.IO.Path.Combine(folder, "artifacts"),
                    new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
            }
            catch (IOException)
            {
                await Task.Delay(Retry, cancellationToken);
            }
        }
    }

    /// <summary>Lets the folder go to the next process.</summary>
    public void Dispose() => held.Dispose();

    private static string FolderOf(string root) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root)))[..16];
}
