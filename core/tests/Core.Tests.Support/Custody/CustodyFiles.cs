using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace Core.Tests.Support.Custody;

/// <summary>A directory under the system's temporary path that holds custody files, each one open only to the user of the process; removed with what is in it on dispose.</summary>
public sealed class CustodyFiles : IDisposable
{
    private static readonly Lazy<CustodyFiles> SharedFiles = new(Standard);

    public CustodyFiles()
    {
        Directory = Path.Combine(Path.GetTempPath(), $"cdg-custody-test-{Guid.NewGuid():N}");
        _ = System.IO.Directory.CreateDirectory(Directory);
    }

    /// <summary>A key and a labels file of the right form, for the tests that start the host and need only that it starts; they live until the process ends.</summary>
    public static CustodyFiles Shared => SharedFiles.Value;

    public string Directory { get; }

    /// <summary>The key of <see cref="Shared"/>.</summary>
    public string KeyPath => Path.Combine(Directory, "key");

    /// <summary>The labels file of <see cref="Shared"/>.</summary>
    public string LabelsPath => Path.Combine(Directory, "labels");

    /// <summary>The bytes nobody would pick by chance that the key of <see cref="Shared"/> holds.</summary>
    public static byte[] SyntheticKey() => [.. Enumerable.Range(0, 32).Select(index => (byte)(0x5A ^ ((index * 41) + 3)))];

    /// <summary>Writes the file open only to the user of the process and returns its path.</summary>
    public string Write(string name, byte[] content)
    {
        var path = Path.Combine(Directory, name);
        var options = new FileStreamOptions { Mode = FileMode.Create, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using (var stream = new FileStream(path, options))
        {
            stream.Write(content);
        }

        Restrict(path);
        return path;
    }

    public string WriteText(string name, string content) => Write(name, new UTF8Encoding(false).GetBytes(content));

    /// <summary>Leaves the file to the user of the process alone: mode 600, or on Windows no inherited entry and one for the user.</summary>
    public static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            RestrictOnWindows(path);
        }
        else
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);

    private static CustodyFiles Standard()
    {
        var files = new CustodyFiles();
        _ = files.Write("key", Encoding.ASCII.GetBytes(Convert.ToBase64String(SyntheticKey()) + "\n"));
        _ = files.Write("labels", []);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => files.Dispose();
        return files;
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictOnWindows(string path)
    {
        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, false, typeof(SecurityIdentifier)))
        {
            _ = security.RemoveAccessRule(rule);
        }

        using var process = WindowsIdentity.GetCurrent();
        security.AddAccessRule(new FileSystemAccessRule(process.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        file.SetAccessControl(security);
    }
}
