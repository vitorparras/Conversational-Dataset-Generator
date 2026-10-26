using System.Security.Cryptography;
using System.Text;
using Core.Host.Supervision;

namespace Core.Host.Tests.Supervision;

/// <summary>A language service folder the way the build leaves it, in a directory of its own: a few files and the manifest that sums them.</summary>
internal sealed class MountedServiceFolder : IDisposable
{
    private const string Interpreter = "python/bin/python3";
    private const string Module = "service/main.py";

    private readonly TemporaryDirectory directory = new();

    public MountedServiceFolder()
    {
        Write(Interpreter, "interpreter");
        Write(Module, "module");
        File.WriteAllText(Folder.Manifest, Manifest(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public static string ModulePath => Module;

    public ServiceFolder Folder => new(directory.Path, windows: false);

    public void Write(string relative, string content)
    {
        var path = Path.Combine(Folder.Root, relative);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Rewrite(string manifest) => File.WriteAllText(Folder.Manifest, manifest);

    public void Dispose() => directory.Dispose();

    private string Manifest() =>
        string.Concat(new[] { Interpreter, Module }.Select(relative =>
            $"{Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Folder.Root, relative))))}  {relative}\n"));
}
