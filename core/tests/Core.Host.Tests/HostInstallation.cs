using System.Collections.ObjectModel;

namespace Core.Host.Tests;

/// <summary>Where the host binary is, where the process is started from, the arguments it receives and the environment variables added to its own.</summary>
internal sealed record HostInstallation(string BinaryDirectory, string WorkingDirectory, IReadOnlyList<string> Arguments)
{
    /// <summary>Variables the process starts with besides the ones it inherits.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = ReadOnlyDictionary<string, string>.Empty;

    /// <summary>The host as built beside the tests, started from its own directory and told to listen on the address.</summary>
    public static HostInstallation Built(Uri baseAddress) =>
        new(AppContext.BaseDirectory, AppContext.BaseDirectory, [UrlsArgument(baseAddress)]);

    public static string UrlsArgument(Uri baseAddress) => $"--Host:Urls={baseAddress.GetLeftPart(UriPartial.Authority)}";

    public string ExecutablePath =>
        Path.Combine(BinaryDirectory, OperatingSystem.IsWindows() ? "Core.Host.exe" : "Core.Host");
}
