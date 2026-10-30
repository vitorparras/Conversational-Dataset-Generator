using Core.Platform;
using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host.Tests;

/// <summary>Every installation declares itself real or a demonstration; the key has no default, takes the two values exactly as written, and nothing else.</summary>
public sealed class InstallationDeclarationTests
{
    [Fact]
    public void KindNotDeclaredIsRefusedAsMissingAndNamesTheKey()
    {
        var loaded = Loaded();

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("Installation:Kind", problem.Key);
    }

    [Fact]
    public void DemonstrationDeclaredMakesADemonstrationInstallation()
    {
        var loaded = Loaded("--Installation:Kind=demonstracao");

        Assert.Equal(new Installation(InstallationKind.Demonstration), InstallationConfiguration.Of(loaded.Values));
    }

    [Fact]
    public void RealDeclaredMakesARealInstallation()
    {
        var loaded = Loaded("--Installation:Kind=real");

        Assert.Equal(new Installation(InstallationKind.Real), InstallationConfiguration.Of(loaded.Values));
    }

    [Theory]
    [InlineData("Real")]
    [InlineData("demo")]
    [InlineData("demonstração")]
    [InlineData("producao")]
    public void AnyOtherValueIsRefusedNamingWhatWasFoundAndWhatIsAccepted(string declared)
    {
        var loaded = Loaded($"--Installation:Kind={declared}");

        var problem = Assert.Single(loaded.Problems);
        Assert.Equal($"valor não aceito para 'Installation:Kind': encontrado '{declared}'; aceitos: real, demonstracao", problem.Describe());
    }

    [Fact]
    public void KindDeclaredWithoutAValueIsRefused()
    {
        var loaded = Loaded("--Installation:Kind=");

        Assert.Equal(ConfigurationProblemKind.EmptyValue, Assert.Single(loaded.Problems).Kind);
    }

    [Fact]
    public void TheShippedSettingsFileDoesNotDeclareTheKind()
    {
        var shipped = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.DoesNotContain("Installation", shipped, StringComparison.Ordinal);
    }

    private static ConfigurationLoadResult Loaded(params string[] arguments) =>
        ClosedConfiguration.Load(new ConfigurationSchema([InstallationConfiguration.Section]), new StrictArgumentsSource(arguments));
}
