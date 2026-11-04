using Core.Platform.Configuration;

namespace Core.Host.Tests;

/// <summary>
/// What the host recognises of a seal destination under the control of the database, one rule at a time (issue #62, part B1, CA-2): the refusal says which rule caught the destination,
/// the other destinations are the installation's declaration, and a destination with a user in it is refused without showing it.
/// </summary>
public sealed class SealConfigurationTests
{
    [Theory]
    [InlineData("http://zqx-vault.example.test/selos", "o esquema não é https")]
    [InlineData("ftp://zqx-vault.example.test/selos", "o esquema não é https")]
    [InlineData("https://localhost/selos", "loopback")]
    [InlineData("https://127.0.0.1/selos", "loopback")]
    [InlineData("https://127.3.4.5:8443/selos", "loopback")]
    [InlineData("https://[::1]/selos", "loopback")]
    [InlineData("/var/cdg/selos", "caminho de arquivo")]
    [InlineData(@"\\servidor\selos", "caminho de arquivo")]
    [InlineData(@"C:\selos\selo.txt", "caminho de arquivo")]
    [InlineData("c:/selos/selo.txt", "caminho de arquivo")]
    [InlineData("file:///var/cdg/selos", "caminho de arquivo")]
    [InlineData("zqx-sem-esquema", "não é um endereço absoluto")]
    [InlineData("https://zqx-user:zqx-secret@zqx-vault.example.test/selos", "usuário ou senha")]
    public void DestinationThatTheHostRecognisesAsUnderTheControlOfTheDatabaseIsRefusedWithTheRuleThatCaughtIt(string destination, string rule)
    {
        var problems = SealConfiguration.Refusals(Values("External", destination));

        var problem = Assert.Single(problems);
        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, SealConfiguration.DestinationPath), (problem.Kind, problem.Key));
        Assert.Contains(rule, Assert.Single(problem.Accepted!), StringComparison.Ordinal);
    }

    [Fact]
    public void ThisMachineByItsNameIsRefusedAsTheMachineItself()
    {
        var problem = Assert.Single(SealConfiguration.Refusals(Values("Internal", $"https://{Environment.MachineName}/selos")));

        Assert.Contains("própria máquina", Assert.Single(problem.Accepted!), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("zqx-db.example.test:5432", "https://zqx-db.example.test/selos")]
    [InlineData("zqx-db.example.test", "https://ZQX-DB.example.test:8443/selos")]
    public void TheServerOfTheDatabaseIsNotADestinationEvenOnAnotherPort(string databaseHost, string destination)
    {
        var values = Values("External", destination);
        values["Database:Host"] = databaseHost;

        var problem = Assert.Single(SealConfiguration.Refusals(values));

        Assert.Contains("mesmo servidor do banco", Assert.Single(problem.Accepted!), StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherServerThanTheDatabaseOneIsTheDeclarationOfTheInstallationAndIsAccepted()
    {
        var values = Values("External", "https://zqx-vault.example.test:8443/selos");
        values["Database:Host"] = "zqx-db.example.test:5432";

        Assert.Empty(SealConfiguration.Refusals(values));
    }

    [Fact]
    public void DestinationWithAUserInItIsRefusedWithoutBeingShownAndOneWithNoneIsShown()
    {
        var withUser = Assert.Single(SealConfiguration.Refusals(Values("External", "https://zqx-user:zqx-secret@zqx-vault.example.test/selos")));
        var plain = Assert.Single(SealConfiguration.Refusals(Values("External", "http://zqx-vault.example.test/selos")));

        Assert.DoesNotContain("zqx-secret", withUser.Describe(), StringComparison.Ordinal);
        Assert.Contains("'http://zqx-vault.example.test/selos'", plain.Describe(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("External")]
    [InlineData("Internal")]
    public void SituationThatSendsTheSealNeedsADestinationAndIsToldAsMissing(string situation)
    {
        var problem = Assert.Single(SealConfiguration.Refusals(new Dictionary<string, string> { [SealConfiguration.SituationPath] = situation }));

        Assert.Equal((ConfigurationProblemKind.MissingKey, SealConfiguration.DestinationPath), (problem.Kind, problem.Key));
    }

    [Fact]
    public void ManualSituationWithADestinationIsRefusedBecauseItHasNoneToSend()
    {
        var problem = Assert.Single(SealConfiguration.Refusals(Values("Manual", "https://zqx-vault.example.test/selos")));

        Assert.Contains("não tem destino", Assert.Single(problem.Accepted!), StringComparison.Ordinal);
    }

    [Fact]
    public void ManualSituationWithNoDestinationAndNoSituationAtAllAreNotRefusedHere()
    {
        var manual = SealConfiguration.Refusals(new Dictionary<string, string> { [SealConfiguration.SituationPath] = "Manual" });
        var none = SealConfiguration.Refusals(new Dictionary<string, string>());

        Assert.Empty(manual.Concat(none));
    }

    [Theory]
    [InlineData("External")]
    [InlineData("Internal")]
    public void DeclarationHoldsTheSituationAndTheDestinationOfThoseThatSend(string situation)
    {
        var declared = SealConfiguration.Declaration(Values(situation, "https://zqx-vault.example.test/selos"));

        Assert.Equal((situation, "https://zqx-vault.example.test/selos"), (declared.Situation.ToString(), declared.Destination!.AbsoluteUri));
    }

    [Fact]
    public void DeclarationOfTheManualSituationHasNoDestination()
    {
        var declared = SealConfiguration.Declaration(new Dictionary<string, string> { [SealConfiguration.SituationPath] = "Manual" });

        Assert.Equal((SealSituation.Manual, null), (declared.Situation, declared.Destination?.AbsoluteUri));
    }

    private static Dictionary<string, string> Values(string situation, string destination) =>
        new() { [SealConfiguration.SituationPath] = situation, [SealConfiguration.DestinationPath] = destination };
}
