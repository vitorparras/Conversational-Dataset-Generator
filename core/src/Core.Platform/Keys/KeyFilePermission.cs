
namespace Core.Platform.Keys;

/// <summary>Whether a custody file is open to anyone besides the process's owner, which the system refuses to read from.</summary>
internal static class KeyFilePermission
{
    private const UnixFileMode GroupAndOthers =
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
        | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;

    private const int PermissionBits = 0x1FF;
    private const int Octal = 8;
    private const int OctalDigits = 3;
    private const string LocalSystemSid = "S-1-5-18";
    private const string AdministratorsSid = "S-1-5-32-544";

    /// <summary>What the file's permission grants beyond the process's owner; null when it grants nothing.</summary>
    internal static string? Excess(string path) =>
        OperatingSystem.IsWindows() ? WindowsKeyFilePermission.Excess(path) : Excess(File.GetUnixFileMode(path));

    /// <summary>Any bit of group or others is more than the owner's; the mode is told in octal.</summary>
    internal static string? Excess(UnixFileMode mode) =>
        (mode & GroupAndOthers) == UnixFileMode.None
            ? null
            : $"permissão aberta a grupo ou outros (modo {Convert.ToString((int)mode & PermissionBits, Octal).PadLeft(OctalDigits, '0')}); só o dono do processo pode ter acesso, como no modo 600";

    /// <summary>The accounts with something allowed beyond the user of the process, SYSTEM and Administrators; the entries that deny or grant no right do not count.</summary>
    internal static string? Excess(IEnumerable<AccessEntry> entries, string userSid)
    {
        var others = entries
            .Where(entry => entry.Allows && entry.Rights != 0 && !MayHold(entry.Sid, userSid))
            .Select(entry => entry.Account)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        return others.Count == 0
            ? null
            : $"permissão concedida a quem não é o usuário do processo, SYSTEM nem Administradores: {string.Join(", ", others)}";
    }

    private static bool MayHold(string sid, string userSid) =>
        sid.Equals(userSid, StringComparison.OrdinalIgnoreCase) || sid is LocalSystemSid or AdministratorsSid;
}
