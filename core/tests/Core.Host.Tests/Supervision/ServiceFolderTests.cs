using Core.Host.Supervision;
using Core.Platform.Configuration;

namespace Core.Host.Tests.Supervision;

/// <summary>Without the key, the interpreter is the one the build leaves in the folder beside the executable; with it, the key wins.</summary>
public sealed class ServiceFolderTests
{
    private static readonly string Base = Path.Combine(Path.GetTempPath(), "cdg-beside");

    [Fact]
    public void WithoutTheKeyTheInterpreterIsTheLinuxOneInTheFolderBesideTheExecutable()
    {
        var folder = new ServiceFolder(Base, windows: false);

        Assert.Equal(Path.Combine(Base, "language-service", "python", "bin", "python3"), folder.Executable(given: null));
    }

    [Fact]
    public void WithoutTheKeyTheInterpreterIsTheWindowsOneInTheFolderBesideTheExecutable()
    {
        var folder = new ServiceFolder(Base, windows: true);

        Assert.Equal(Path.Combine(Base, "language-service", "python", "python.exe"), folder.Executable(given: null));
    }

    [Fact]
    public void WithTheKeyTheExecutableGivenIsTheOneUsed()
    {
        var folder = new ServiceFolder(Base, windows: false);

        Assert.Equal("zqx-given-7731", folder.Executable("zqx-given-7731"));
    }

    [Fact]
    public void ConfigurationWithoutTheKeyLoadsAndGivesNoExecutable()
    {
        var loaded = ClosedConfiguration.Load(
            new ConfigurationSchema([LanguageServiceConfiguration.Section]));

        Assert.False(loaded.Refused);
        Assert.Null(LanguageServiceConfiguration.GivenExecutable(loaded.Values));
    }
}
