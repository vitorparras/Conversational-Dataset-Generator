using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Core.Platform.Keys;

/// <summary>The permission of a custody file on Windows: only the user of the process, SYSTEM and Administrators may be granted anything.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsKeyFilePermission
{
    /// <summary>The accounts the file grants something to besides the three that may hold it; null when there are none.</summary>
    internal static string? Excess(string path)
    {
        var granted = new FileInfo(path).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Allow)
            .Select(rule => (SecurityIdentifier)rule.IdentityReference);
        using var process = WindowsIdentity.GetCurrent();
        return Excess(granted, process.User!);
    }

    internal static string? Excess(IEnumerable<SecurityIdentifier> granted, SecurityIdentifier user)
    {
        var others = granted.Where(sid => !MayHold(sid, user)).Select(Named).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return others.Count == 0
            ? null
            : $"permissão concedida a quem não é o usuário do processo, SYSTEM nem Administradores: {string.Join(", ", others)}";
    }

    private static bool MayHold(SecurityIdentifier sid, SecurityIdentifier user) =>
        sid.Equals(user) || sid.IsWellKnown(WellKnownSidType.LocalSystemSid) || sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);

    private static string Named(SecurityIdentifier sid)
    {
        try
        {
            return sid.Translate(typeof(NTAccount)).Value;
        }
        catch (IdentityNotMappedException)
        {
            return sid.Value;
        }
    }
}
