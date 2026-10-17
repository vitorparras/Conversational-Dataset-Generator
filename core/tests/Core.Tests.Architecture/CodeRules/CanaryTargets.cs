using System.Security;

namespace Core.Tests.Architecture.CodeRules;

/// <summary>The MSBuild file that turns a real project into its own canary: it adds the canary sources, and the logging package when the project lacks it, to that project and to no other.</summary>
internal static class CanaryTargets
{
    public const string FragmentFileName = "fragment.targets";
    private const string LoggingPackage = "Microsoft.Extensions.Logging.Abstractions";

    public static string Text(string projectPath, IEnumerable<string> canaryFiles, IEnumerable<string> replacedSources, (string Scratch, string Canary) folders)
    {
        var (scratchPath, canaryDirectory) = folders;
        var fragmentPath = Path.Combine(scratchPath, FragmentFileName);
        var lockFiles = Path.Combine(scratchPath, "locks");
        var applies = $"'$(MSBuildProjectFullPath)' == '{SecurityElement.Escape(projectPath)}'";
        var compiles = string.Join(Environment.NewLine, canaryFiles.Select(file => $"    <Compile Include=\"{SecurityElement.Escape(file)}\" />"));
        var replaces = string.Join(Environment.NewLine, replacedSources.Select(file => $"    <Compile Remove=\"{SecurityElement.Escape(file)}\" />"));
        return $$"""
            <Project>
              <PropertyGroup>
                <!-- Restore writes its lock file here, so that no lock file of the repository is read or changed. -->
                <NuGetLockFilePath>{{SecurityElement.Escape(lockFiles)}}/$(MSBuildProjectName).lock.json</NuGetLockFilePath>
              </PropertyGroup>
              <PropertyGroup Condition="{{applies}}">
                <CanaryDirectory>{{SecurityElement.Escape(canaryDirectory)}}</CanaryDirectory>
                <!-- A project that gets the logging package from its framework warns that the reference is not needed; the canary needs it anyway. -->
                <NoWarn>$(NoWarn);NU1510</NoWarn>
              </PropertyGroup>
              <ItemGroup Condition="{{applies}}">
            {{compiles}}
            {{replaces}}
                <PackageReference Include="{{LoggingPackage}}" Condition="'@(PackageReference->WithMetadataValue('Identity','{{LoggingPackage}}'))' == ''" />
              </ItemGroup>
              <Import Project="{{SecurityElement.Escape(fragmentPath)}}" Condition="{{applies}}" />
            </Project>
            """;
    }
}
