namespace Core.Host.Supervision;

/// <summary>Where the language service folder lies: beside the executable, with the interpreter and the manifest at the places the build puts them.</summary>
internal sealed class ServiceFolder
{
    private const string FolderName = "language-service";
    private const string ManifestName = "SHA256SUMS";

    internal ServiceFolder(string baseDirectory, bool windows)
    {
        Root = Path.GetFullPath(Path.Combine(baseDirectory, FolderName));
        Manifest = Path.Combine(Root, ManifestName);
        Interpreter = windows
            ? Path.Combine(Root, "python", "python.exe")
            : Path.Combine(Root, "python", "bin", "python3");
    }

    /// <summary>Where the manifest is said to come from, in the form the operator reads it.</summary>
    internal static string ManifestSource => FolderName + "/" + ManifestName;

    internal string Root { get; }

    internal string Manifest { get; }

    internal string Interpreter { get; }

    /// <summary>The folder beside the executable that runs, for the system it runs on.</summary>
    internal static ServiceFolder Beside() => new(AppContext.BaseDirectory, OperatingSystem.IsWindows());

    /// <summary>The executable the configuration gave, or else the interpreter of the folder.</summary>
    internal string Executable(string? given) => given ?? Interpreter;

    /// <summary>The place of a path the manifest names, or null when it leaves the folder.</summary>
    internal string? Inside(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
    }

    /// <summary>A name for the operator: the folder and the path the manifest gave.</summary>
    internal static string Named(string relative) => FolderName + "/" + relative;
}
