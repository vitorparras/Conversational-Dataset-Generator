using Core.Host.Tests.Logging;
using Core.Platform;
using Core.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>A real installation declares when its recovery was last tested and who holds the copy; a demonstration declares neither, and the real one's declaration reaches the startup line.</summary>
[Collection("console")]
public sealed class SafekeepingConfigurationTests
{
    private const string Date = "2026-10-01";
    private const string Holder = "PESSOA_1";

    [Fact]
    public void RealInstallationWithoutTheDeclarationIsRefusedNamingBothKeys()
    {
        var refusals = SafekeepingConfiguration.Refusals(Values(), InstallationKind.Real);

        Assert.Equal([SafekeepingConfiguration.LastRecoveryTestPath, SafekeepingConfiguration.HolderPath], refusals.Select(problem => problem.Key));
        Assert.All(refusals, problem => Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind));
    }

    [Fact]
    public void RealInstallationWithoutTheDateIsRefusedNamingTheDateOnly()
    {
        var refusals = SafekeepingConfiguration.Refusals(Values(holder: Holder), InstallationKind.Real);

        Assert.Equal(SafekeepingConfiguration.LastRecoveryTestPath, Assert.Single(refusals).Key);
    }

    [Fact]
    public void RealInstallationWithoutTheHolderIsRefusedNamingTheHolderOnly()
    {
        var refusals = SafekeepingConfiguration.Refusals(Values(date: Date), InstallationKind.Real);

        Assert.Equal(SafekeepingConfiguration.HolderPath, Assert.Single(refusals).Key);
    }

    [Theory]
    [InlineData("01/10/2026")]
    [InlineData("2026-13-01")]
    [InlineData("2026-10-1")]
    [InlineData("2026-10-01T00:00:00")]
    [InlineData("ontem")]
    public void RealInstallationWithADateThatIsNotIsoIsRefusedSayingWhatItFound(string date)
    {
        var problem = Assert.Single(SafekeepingConfiguration.Refusals(Values(date, Holder), InstallationKind.Real));

        Assert.Equal(
            $"valor não aceito para 'Custody:LastRecoveryTest': encontrado '{date}'; aceitos: uma data ISO, yyyy-MM-dd",
            problem.Describe());
    }

    [Fact]
    public void RealInstallationWithTheDeclarationIsNotRefused() =>
        Assert.Empty(SafekeepingConfiguration.Refusals(Values(Date, Holder), InstallationKind.Real));

    [Fact]
    public void DemonstrationNeedsNoDeclaration() =>
        Assert.Empty(SafekeepingConfiguration.Refusals(Values(), InstallationKind.Demonstration));

    [Fact]
    public void DeclarationHoldsTheDateAndTheHolderAsWritten()
    {
        var declaration = SafekeepingConfiguration.Declaration(Values(Date, Holder));

        Assert.Equal(new SafekeepingDeclaration(Date, Holder), declaration);
    }

    [Fact]
    public void DeclarationIsNoneWhenOneOfTheKeysIsMissing()
    {
        Assert.Null(SafekeepingConfiguration.Declaration(Values(date: Date)));
        Assert.Null(SafekeepingConfiguration.Declaration(Values(holder: Holder)));
    }

    [Fact]
    public void StartupLineOfARealInstallationCarriesTheDateAndErasesTheHolder()
    {
        var line = ComposedConsole.LineWrittenBy(logger => InstallationConfiguration.Announce(
            logger,
            new ServiceCollection().AddSingleton(new Installation(InstallationKind.Real)).AddSingleton(new SafekeepingDeclaration(Date, Holder)).BuildServiceProvider()));

        Assert.Contains("Instalação real", line, StringComparison.Ordinal);
        Assert.Contains($"LastRecoveryTest={Date}", line, StringComparison.Ordinal);
        Assert.Contains("Holder=", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Holder, line, StringComparison.Ordinal);
    }

    private static Dictionary<string, string> Values(string? date = null, string? holder = null)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (date is not null)
        {
            values[SafekeepingConfiguration.LastRecoveryTestPath] = date;
        }

        if (holder is not null)
        {
            values[SafekeepingConfiguration.HolderPath] = holder;
        }

        return values;
    }
}
