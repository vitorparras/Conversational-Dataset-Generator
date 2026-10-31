using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Core.Platform.Keys;
using Core.Tests.Support.Custody;

namespace Core.Platform.Tests.Keys;

/// <summary>A custody file open to anyone besides the owner of the process is refused, on both systems, and the refusal says what it saw.</summary>
public sealed class KeyFilePermissionTests : IDisposable
{
    private readonly CustodyFiles files = new();

    public void Dispose() => files.Dispose();

    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite)]
    [InlineData(UnixFileMode.UserRead)]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute)]
    public void ModeOfTheOwnerAloneGrantsNothingBeyondTheOwner(UnixFileMode mode) =>
        Assert.Null(KeyFilePermission.Excess(mode));

    [Theory]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead, "640")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead, "604")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.GroupWrite, "420")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherExecute, "601")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupExecute, "610")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherWrite, "602")]
    [InlineData(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead, "644")]
    public void AnyBitOfGroupOrOthersIsRefusedNamingTheModeInOctal(UnixFileMode mode, string octal) =>
        Assert.Equal($"permissão aberta a grupo ou outros (modo {octal}); só o dono do processo pode ter acesso, como no modo 600", KeyFilePermission.Excess(mode));

    [Fact]
    public void ModeWithASpecialBitIsToldWithoutIt() =>
        Assert.Contains("(modo 604)", KeyFilePermission.Excess(UnixFileMode.SetUser | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead), StringComparison.Ordinal);

    [Fact]
    public void FileWrittenForTheOwnerAloneGrantsNothingBeyondTheOwner() =>
        Assert.Null(KeyFilePermission.Excess(files.WriteText("so-dono", "x")));

    [Fact]
    public void FileOnLinuxOpenToTheGroupIsRefusedNamingTheMode()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "o modo de arquivo é do Linux");
        var path = files.WriteText("grupo", "x");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        Assert.Contains("(modo 640)", KeyFilePermission.Excess(path), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FileOnLinuxOpenToOthersIsRefusedByTheSource()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "o modo de arquivo é do Linux");
        var key = files.WriteText("key", Convert.ToBase64String(SyntheticKey.Valid()));
        var labels = files.WriteText("labels", string.Empty);
        File.SetUnixFileMode(labels, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);

        using var reading = await new FileMasterKeySource(key, labels, AppContext.BaseDirectory).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo dos rótulos em '{labels}': permissão aberta a grupo ou outros (modo 604)", reading.Failure.Found, StringComparison.Ordinal);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void EntryForTheUserOfTheProcessAloneGrantsNothingBeyondTheOwner()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        using var process = WindowsIdentity.GetCurrent();

        Assert.Null(WindowsKeyFilePermission.Excess([process.User!], process.User!));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void EntriesForSystemAndAdministratorsGrantNothingBeyondTheOwner()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        using var process = WindowsIdentity.GetCurrent();
        SecurityIdentifier[] granted =
        [
            process.User!,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
        ];

        Assert.Null(WindowsKeyFilePermission.Excess(granted, process.User!));
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void EntryForAnotherAccountIsRefusedNamingTheAccount()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        using var process = WindowsIdentity.GetCurrent();
        SecurityIdentifier[] granted = [process.User!, new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null)];

        var excess = WindowsKeyFilePermission.Excess(granted, process.User!);

        Assert.Equal($"permissão concedida a quem não é o usuário do processo, SYSTEM nem Administradores: {NameOf(WellKnownSidType.BuiltinUsersSid)}", excess);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void AccountsThatCannotBeNamedAreToldBySid()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        using var process = WindowsIdentity.GetCurrent();
        var unknown = new SecurityIdentifier("S-1-5-21-1-2-3-4242");

        var excess = WindowsKeyFilePermission.Excess([unknown, unknown], process.User!);

        Assert.Equal("permissão concedida a quem não é o usuário do processo, SYSTEM nem Administradores: S-1-5-21-1-2-3-4242", excess);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void FileOnWindowsWithAnEntryForEveryoneIsRefusedByTheSource()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        var path = files.WriteText("aberto", "x");
        var file = new FileInfo(path);
        var security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(security);

        var excess = KeyFilePermission.Excess(path);

        Assert.Contains(NameOf(WellKnownSidType.WorldSid), excess, StringComparison.Ordinal);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public void FileOnWindowsWithAnInheritedEntryForUsersIsRefused()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        var directory = new DirectoryInfo(files.Directory);
        var parent = directory.GetAccessControl();
        parent.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Read, InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        directory.SetAccessControl(parent);
        var inheriting = Path.Combine(files.Directory, "herdado");
        File.WriteAllText(inheriting, "x");

        Assert.Contains(NameOf(WellKnownSidType.BuiltinUsersSid), KeyFilePermission.Excess(inheriting), StringComparison.Ordinal);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task FileOnWindowsOpenToUsersIsRefusedByTheSourceNamingTheFile()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "a lista de acesso é do Windows");
        var key = files.WriteText("key", Convert.ToBase64String(SyntheticKey.Valid()));
        var labels = files.WriteText("labels", string.Empty);
        var file = new FileInfo(key);
        var security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(security);

        using var reading = await new FileMasterKeySource(key, labels, AppContext.BaseDirectory).ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(MasterKeyFailureKind.Unreadable, reading.Failure!.Kind);
        Assert.Contains($"arquivo da chave em '{key}': permissão concedida a quem não é o usuário do processo", reading.Failure.Found, StringComparison.Ordinal);
    }

    [SupportedOSPlatform("windows")]
    private static string NameOf(WellKnownSidType type) => new SecurityIdentifier(type, null).Translate(typeof(NTAccount)).Value;
}
