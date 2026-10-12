using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Core.Tests.Shared;

namespace Core.Tests.Integration.Host;

/// <summary>
/// A private copy of the built host, so each test writes its own configuration file next to the
/// executable exactly as an operator would, without touching the build output.
/// </summary>
internal sealed class HostInstallation : IDisposable
{
    private HostInstallation(DirectoryInfo directory)
    {
        Directory = directory;
    }

    public DirectoryInfo Directory { get; }

    public string AssemblyPath => Path.Combine(Directory.FullName, "Core.Host.dll");

    public string SettingsPath => Path.Combine(Directory.FullName, "appsettings.json");

    public static string BuildOutput =>
        Path.Combine(
            RepositoryRoot.Find().FullName,
            "core", "src", "Core.Host", "bin", RepositoryRoot.Configuration, "net10.0");

    public static HostInstallation Create()
    {
        var target = System.IO.Directory.CreateTempSubdirectory("cdg-host-");
        Copy(new DirectoryInfo(BuildOutput), target);
        return new HostInstallation(target);
    }

    /// <summary>
    /// A free loopback port below the range the system hands out to anyone asking for "any port".
    /// The language service the host starts asks for one of those, and a port given back by the
    /// test is the first the system offers again, so the two would collide now and then.
    /// </summary>
    public static int FreePort()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var port = System.Security.Cryptography.RandomNumberGenerator.GetInt32(20_000, 30_000);
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
                // In use by something else: try another.
            }
        }

        throw new InvalidOperationException("no free loopback port found in 20000-29999");
    }

    /// <summary>Settings that only move the port, so parallel hosts do not collide.</summary>
    public static string SettingsWithPort(int port) =>
        string.Create(CultureInfo.InvariantCulture, $$"""{ "Server": { "Port": "{{port}}" } }""");

    public void WriteSettings(string json) => File.WriteAllText(SettingsPath, json);

    /// <summary>
    /// Starts the host. The language service is a required part of it, so unless the test says
    /// otherwise the host is given the stand-in service; a test's own variables win over that.
    /// </summary>
    public HostProcess Start(
        IEnumerable<string>? arguments = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        LanguageServiceSetup? languageService = null)
    {
        var variables = new Dictionary<string, string?>((languageService ?? LanguageServiceSetup.Fake()).AsEnvironment());
        foreach (var (name, value) in environment ?? new Dictionary<string, string?>())
        {
            variables[name] = value;
        }

        return HostProcess.Start(AssemblyPath, arguments ?? [], variables);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(recursive: true);
        }
        catch (IOException)
        {
            // A process that just exited may still hold a file on Windows; the temp folder is cleaned later.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above.
        }
    }

    private static void Copy(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var file in source.GetFiles())
        {
            _ = file.CopyTo(Path.Combine(target.FullName, file.Name));
        }

        foreach (var child in source.GetDirectories())
        {
            Copy(child, target.CreateSubdirectory(child.Name));
        }
    }
}
