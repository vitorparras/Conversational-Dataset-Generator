using Core.Host.Tests.Logging;
using Core.Platform;
using Core.Platform.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Host.Tests;

/// <summary>A real installation declares when its recovery was last tested and who holds the copy; a demonstration declares neither, and the real one's declaration reaches the startup line.</summary>
[Collection("console")]
public sealed class CustodyConfigurationTests
{
    private const string Date = "2026-10-01";
    private const string Holder = "PESSOA_1";

    [Fact]
    public void RealInstallationWithoutTheDeclarationIsRefusedNamingBothKeys()
    {
        var refusals = CustodyConfiguration.Refusals(Values(), InstallationKind.Real);

        Assert.Equal([CustodyConfiguration.LastRecoveryTestPath, CustodyConfiguration.HolderPath], refusals.Select(problem => problem.Key));
        Assert.All(refusals, problem => Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind));
    }

    [Fact]
    public void RealInstallationWithoutTheDateIsRefusedNamingTheDateOnly()
    {
        var refusals = CustodyConfiguration.Refusals(Values(holder: Holder), InstallationKind.Real);

        Assert.Equal(CustodyConfiguration.LastRecoveryTestPath, Assert.Single(refusals).Key);
    }

    [Fact]
    public void RealInstallationWithoutTheHolderIsRefusedNamingTheHolderOnly()
    {
        var refusals = CustodyConfiguration.Refusals(Values(date: Date), InstallationKind.Real);

        Assert.Equal(CustodyConfiguration.HolderPath, Assert.Single(refusals).Key);
    }

    [Theory]
    [InlineData("01/10/2026")]
    [InlineData("2026-13-01")]
    [InlineData("2026-10-1")]
    [InlineData("2026-10-01T00:00:00")]
    [InlineData("ontem")]
    public void RealInstallationWithADateThatIsNotIsoIsRefusedSayingWhatItFound(string date)
    {
        var problem = Assert.Single(CustodyConfiguration.Refusals(Values(date, Holder), InstallationKind.Real));

        Assert.Equal(
            $"valor não aceito para 'Custody:LastRecoveryTest': encontrado '{date}'; aceitos: uma data ISO, yyyy-MM-dd",
            problem.Describe());
    }

    [Fact]
    public void RealInstallationWithTheDeclarationIsNotRefused() =>
        Assert.Empty(CustodyConfiguration.Refusals(Values(Date, Holder), InstallationKind.Real));

    [Fact]
    public void DemonstrationNeedsNoDeclaration() =>
        Assert.Empty(CustodyConfiguration.Refusals(Values(), InstallationKind.Demonstration));

    [Fact]
    public void DeclarationHoldsTheDateAndTheHolderAsWritten()
    {
        var declaration = CustodyConfiguration.Declaration(Values(Date, Holder));

        Assert.Equal(new CustodyDeclaration(Date, Holder), declaration);
    }

    [Fact]
    public void DeclarationIsNoneWhenOneOfTheKeysIsMissing()
    {
        Assert.Null(CustodyConfiguration.Declaration(Values(date: Date)));
        Assert.Null(CustodyConfiguration.Declaration(Values(holder: Holder)));
    }

    [Fact]
    public void StartupLineOfARealInstallationCarriesTheDateAndErasesTheHolder()
    {
        var line = ComposedConsole.LineWrittenBy(logger => InstallationConfiguration.Announce(
            logger,
            new ServiceCollection().AddSingleton(new Installation(InstallationKind.Real)).AddSingleton(new CustodyDeclaration(Date, Holder)).BuildServiceProvider()));

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
            values[CustodyConfiguration.LastRecoveryTestPath] = date;
        }

        if (holder is not null)
        {
            values[CustodyConfiguration.HolderPath] = holder;
        }

        return values;
    }
}
