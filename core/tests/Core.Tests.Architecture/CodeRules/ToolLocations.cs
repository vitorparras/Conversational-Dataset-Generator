using System.Runtime.InteropServices;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>Where the running <c>dotnet</c> keeps its SDK, its packs and its package cache, asked of the tool itself and never of the project under test.</summary>
internal static class ToolLocations
{
    private static readonly Lazy<string> Root = new(() => Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..")));
    private static readonly Lazy<string> Sdk = new(FindSdk);
    private static readonly Lazy<string> Packages = new(FindPackages);

    public static string DotnetRoot => Root.Value;

    public static string SdkDirectory => Sdk.Value;

    /// <summary>The global package cache of the machine.</summary>
    public static string GlobalPackages => Packages.Value;

    private static string FindSdk()
    {
        var version = Ask("--version").Trim();
        return Path.Combine(Root.Value, "sdk", version);
    }

    private static string FindPackages()
    {
        const string prefix = "global-packages:";
        var line = Ask("nuget", "locals", "global-packages", "--list").Split('\n').Select(text => text.Trim()).First(text => text.StartsWith(prefix, StringComparison.Ordinal));
        return line[prefix.Length..].Trim();
    }

    private static string Ask(params string[] arguments) =>
        DotnetCommand.RunAsync(arguments, SourceTree.CoreRoot, CancellationToken.None).GetAwaiter().GetResult().OutputOfSuccess($"dotnet {arguments[0]}");
}
