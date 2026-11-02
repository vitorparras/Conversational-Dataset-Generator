using System.Runtime.InteropServices;

namespace Core.Persistence.ServerGuarantees;

/// <summary>
/// Whether the process may write in a folder, asked of the system with access(2) so that the answer is the one the system would give a write
/// and nothing is created or removed to find it out. Only Linux is asked: the server and the application run there.
/// </summary>
internal static partial class WritableFolder
{
    private const int WriteOk = 2;

    /// <summary>True when the system lets the process write in the folder; false when it does not, or when the folder is not there.</summary>
    internal static bool Allows(string path) => OperatingSystem.IsLinux() && Access(path, WriteOk) == 0;

    [LibraryImport("libc", EntryPoint = "access", StringMarshalling = StringMarshalling.Utf8)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    private static partial int Access(string path, int mode);
}
