namespace Core.Platform.Tests.Logging;

/// <summary>Reads a file another writer may still hold open, and tells whether anyone still does.</summary>
internal static class SharedFile
{
    /// <summary>The content as it is on disk now, read without disturbing the writer.</summary>
    public static string Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Opens the file exclusively; this only succeeds once every writer has closed it.</summary>
    public static bool IsClosed(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
