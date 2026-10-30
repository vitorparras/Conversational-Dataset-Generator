using System.Collections.ObjectModel;

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

    /// <summary>Whether the host asks the real language service, which it then starts, instead of the fake recognizer, which needs none.</summary>
    public bool UsesRealService { get; init; }

    /// <summary>
    /// The arguments given plus, unless the test withholds them, the fake recognizer with an interpreter the host never
    /// starts, or the interpreter of the language service when the test asks for the real one.
    /// </summary>
    public IReadOnlyList<string> LaunchArguments => DeclaresInstallation ? [.. ServiceArguments, "--Installation:Kind=demonstracao"] : ServiceArguments;

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
