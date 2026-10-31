namespace Core.Platform.Keys;

/// <summary>Whether a custody file is open to anyone besides the process's owner, which the system refuses to read from.</summary>
internal static class KeyFilePermission
{
    private const UnixFileMode GroupAndOthers =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    private const int PermissionBits = 0x1FF;

    /// <summary>What the file's permission grants beyond the process's owner; null when it grants nothing.</summary>
    internal static string? Excess(string path) =>
        OperatingSystem.IsWindows() ? WindowsKeyFilePermission.Excess(path) : Excess(File.GetUnixFileMode(path));

    /// <summary>Any bit of group or others is more than the owner's; the mode is told in octal.</summary>
    internal static string? Excess(UnixFileMode mode) =>
        (mode & GroupAndOthers) == UnixFileMode.None
            ? null
            : $"permissão aberta a grupo ou outros (modo {Convert.ToString((int)mode & PermissionBits, 8).PadLeft(3, '0')}); só o dono do processo pode ter acesso, como no modo 600";
}
