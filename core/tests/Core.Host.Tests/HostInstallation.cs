using System.Collections.ObjectModel;

namespace Core.Host.Tests;

/// <summary>Where the host binary is, where the process is started from, the arguments it receives and the environment variables added to its own.</summary>
internal sealed record HostInstallation(string BinaryDirectory, string WorkingDirectory, IReadOnlyList<string> Arguments)
{
    /// <summary>Variables the process starts with besides the ones it inherits.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>Whether the arguments the host receives also name the interpreter that starts the language service.</summary>
    public bool ProvidesLanguageService { get; init; } = true;

    /// <summary>The arguments given plus, unless the test withholds it, the interpreter of the language service.</summary>
    public IReadOnlyList<string> LaunchArguments =>
        ProvidesLanguageService ? [.. Arguments, $"--LanguageService:Executable={LanguageServiceInterpreter.Path}"] : Arguments;

    /// <summary>The host as built beside the tests, started from its own directory and told to listen on the address.</summary>
    public static HostInstallation Built(Uri baseAddress) =>
        new(AppContext.BaseDirectory, AppContext.BaseDirectory, [UrlsArgument(baseAddress)]);

    public static string UrlsArgument(Uri baseAddress) => $"--Host:Urls={baseAddress.GetLeftPart(UriPartial.Authority)}";

    /// <summary>The same installation told to write its log also to the file at the path.</summary>
    public HostInstallation WithLogFile(string path) => this with { Arguments = [.. Arguments, $"--Host:LogFile={path}"] };

    public string ExecutablePath =>
        Path.Combine(BinaryDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host");
}
