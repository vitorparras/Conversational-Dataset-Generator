using System.Collections.ObjectModel;
using Core.Tests.Support.Audit;
using Core.Tests.Support.Custody;
using Core.Tests.Support.Net;

namespace Core.Host.Tests;

/// <summary>Where the host binary is, where the process is started from, the arguments it receives and the environment variables added to its own.</summary>
internal sealed record HostInstallation(string BinaryDirectory, string WorkingDirectory, IReadOnlyList<string> Arguments)
{
    /// <summary>Variables the process starts with besides the ones it inherits.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>Whether the arguments the host receives also choose the recognizer and name an interpreter.</summary>
    public bool ProvidesLanguageService { get; init; } = true;

    /// <summary>Whether the arguments the host receives also declare the installation a demonstration, which the host cannot start without declaring one.</summary>
    public bool DeclaresInstallation { get; init; } = true;

    /// <summary>Whether the arguments the host receives also give the work factor of the derivation of passwords, which the serving mode cannot start without.</summary>
    public bool DeclaresWorkFactor { get; init; } = true;

    /// <summary>Whether the arguments the host receives also give the two limits of a session, which the serving mode cannot start without.</summary>
    public bool DeclaresSessionLimits { get; init; } = true;

    /// <summary>Whether the arguments the host receives also name the certificate and the private key of the encrypted channel, which the serving mode cannot start without.</summary>
    public bool ProvidesTlsMaterial { get; init; } = true;

    /// <summary>Whether the arguments the host receives also name the folder of the database server's log, which the serving mode cannot start without.</summary>
    public bool ProvidesServerLog { get; init; } = true;

    /// <summary>Whether the arguments the host receives also name the custody files of a key and labels of the right form, which the serving mode cannot start without.</summary>
    public bool ProvidesMasterKey { get; init; } = true;

    /// <summary>Whether the host asks the real language service, which it then starts, instead of the fake recognizer, which needs none.</summary>
    public bool UsesRealService { get; init; }

    /// <summary>
    /// The arguments given plus, unless the test withholds them, the fake recognizer with an interpreter the host never
    /// starts, or the interpreter of the language service when the test asks for the real one.
    /// </summary>
    public IReadOnlyList<string> LaunchArguments
    {
        get
        {
            var arguments = new List<string>(ServiceArguments);
            if (DeclaresWorkFactor)
            {
                arguments.Add(WorkFactorArgument);
            }

            if (DeclaresSessionLimits)
            {
                arguments.AddRange(SessionArguments);
            }

            if (ProvidesTlsMaterial)
            {
                arguments.AddRange(TlsArguments);
            }

            if (ProvidesServerLog)
            {
                arguments.Add(ServerLogArgument);
            }

            if (!DeclaresInstallation)
            {
                return arguments;
            }

            arguments.Add("--Installation:Kind=demonstracao");
            if (ProvidesMasterKey)
            {
                arguments.AddRange(MasterKeyArguments);
            }

            return arguments;
        }
    }

    /// <summary>The argument that gives the work factor; the host takes any whole number above zero, and a test that does not hash a password wants the cheapest.</summary>
    public static string WorkFactorArgument => "--Passwords:WorkFactor=1";

    /// <summary>The limits of a session a test that does not wait for one to pass wants: long enough that none passes while it runs.</summary>
    public static IReadOnlyList<string> SessionArguments => ["--Session:IdleTimeout=00:30:00", "--Session:AbsoluteTimeout=08:00:00"];

    /// <summary>The argument that points the host at the folder of a server log it can read.</summary>
    public static string ServerLogArgument => $"--Audit:ServerLogDirectory={ServerLogFiles.Shared}";

    /// <summary>The arguments that point the host at the shared certificate and private key.</summary>
    public static IReadOnlyList<string> TlsArguments =>
        [$"--Tls:CertificateFile={SharedTlsMaterial.CertificatePath}", $"--Tls:KeyFile={SharedTlsMaterial.KeyPath}"];

    /// <summary>The arguments that point the host at the shared key and labels files.</summary>
    public static IReadOnlyList<string> MasterKeyArguments =>
        [$"--MasterKey:File={CustodyFiles.Shared.KeyPath}", $"--MasterKey:LabelsFile={CustodyFiles.Shared.LabelsPath}"];

    private IReadOnlyList<string> ServiceArguments => (ProvidesLanguageService, UsesRealService) switch
    {
        (false, _) => Arguments,
        (true, true) => [.. Arguments, $"--LanguageService:Executable={LanguageServiceInterpreter.Path}"],
        (true, false) => [.. Arguments, "--Recognizer:Kind=fake", "--LanguageService:Executable=zqx-never-started-7731"],
    };

    /// <summary>The host as built beside the tests, started from its own directory and told to listen on the address.</summary>
    public static HostInstallation Built(Uri baseAddress) =>
        new(AppContext.BaseDirectory, AppContext.BaseDirectory, [UrlsArgument(baseAddress)]);

    public static string UrlsArgument(Uri baseAddress) => $"--Host:Urls={baseAddress.GetLeftPart(UriPartial.Authority)}";

    /// <summary>The same installation told to write its log also to the file at the path.</summary>
    public HostInstallation WithLogFile(string path) => this with { Arguments = [.. Arguments, $"--Host:LogFile={path}"] };

    public string ExecutablePath =>
        Path.Combine(BinaryDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host");
}
