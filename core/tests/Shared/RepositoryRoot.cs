namespace Core.Tests.Shared;

/// <summary>Locates the repository from the test output directory.</summary>
internal static class RepositoryRoot
{
    /// <summary>The directory that contains <c>core/Core.slnx</c>.</summary>
    public static DirectoryInfo Find()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "core", "Core.slnx")))
            {
                return current;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("core/Core.slnx not found above the test output.");
    }

    /// <summary>The build configuration the tests were compiled with.</summary>
    public static string Configuration =>
#if DEBUG
        "Debug";
#else
        "Release";
#endif
}
