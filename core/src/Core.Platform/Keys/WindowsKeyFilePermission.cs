using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Core.Platform.Keys;

/// <summary>Reads the access list of a custody file on Windows; the rule that judges it is <see cref="KeyFilePermission"/>'s, the same on any system.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsKeyFilePermission
{
    /// <summary>What the access list of the file grants beyond the user of the process, SYSTEM and Administrators; null when nothing.</summary>
    internal static string? Excess(string path)
    {
        var entries = new FileInfo(path).GetAccessControl()
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Select(rule => new AccessEntry(rule.IdentityReference.Value, Named(rule.IdentityReference), rule.AccessControlType == AccessControlType.Allow, (int)rule.FileSystemRights));
        using var process = WindowsIdentity.GetCurrent();
        return KeyFilePermission.Excess(entries, process.User!.Value);
    }

    private static string Named(IdentityReference sid)
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
