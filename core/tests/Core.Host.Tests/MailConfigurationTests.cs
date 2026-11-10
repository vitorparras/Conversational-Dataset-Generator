using Core.Platform.Configuration;
using Core.Platform.Configuration.Sources;

namespace Core.Host.Tests;

/// <summary>
/// The optional server of e-mail (issue 635): with no key of the group the host starts and the reset of a password is handed over by hand (CA-1); declared in part, with a port that is
/// none, or with a transport in clear, the start is refused naming every key at fault (CA-2); and the password is shown by its name and origin, never its value (CA-3).
/// </summary>
public sealed class MailConfigurationTests
{
    [Fact]
    public void Ca1WithNoKeyOfTheGroupNothingIsRefusedAndNoServerIsDeclared()
    {
        var loaded = Load([]);

        Assert.False(loaded.Refused);
        Assert.Empty(MailConfiguration.Refusals(loaded.Values));
        Assert.Null(MailConfiguration.Declaration(loaded.Values));
    }

    [Fact]
    public void AWholeGroupIsTheServerTheInstallationDeclared()
    {
        var loaded = Load(Whole());

        Assert.False(loaded.Refused);
        Assert.Empty(MailConfiguration.Refusals(loaded.Values));
        Assert.Equal(new MailServer("zqx-smtp.example.test", 587, "zqx@example.test", MailTransport.StartTls, "zqx-user"), MailConfiguration.Declaration(loaded.Values));
    }

    [Fact]
    public void AGroupWithNoUserNorPasswordIsWholeForAServerThatAsksForNone()
    {
        var values = Whole();
        _ = values.Remove("CDG_Mail__User");
        _ = values.Remove("CDG_Mail__Password");

        var loaded = Load(values);

        Assert.Empty(MailConfiguration.Refusals(loaded.Values));
        Assert.Null(MailConfiguration.Declaration(loaded.Values)!.User);
    }

    [Fact]
    public void Ca2AGroupDeclaredInPartIsRefusedNamingEveryKeyMissing()
    {
        var loaded = Load(new Dictionary<string, string?> { ["CDG_Mail__Host"] = "zqx-smtp.example.test" });

        var missing = MailConfiguration.Refusals(loaded.Values);

        Assert.All(missing, problem => Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind));
        Assert.Equal([MailConfiguration.PortPath, MailConfiguration.SenderPath, MailConfiguration.TransportPath], missing.Select(problem => problem.Key));
    }

    [Theory]
    [InlineData("CDG_Mail__Password", MailConfiguration.PasswordPath)]
    [InlineData("CDG_Mail__User", MailConfiguration.UserPath)]
    public void Ca2AUserWithoutItsPasswordOrAPasswordWithoutItsUserIsRefused(string removed, string named)
    {
        var values = Whole();
        _ = values.Remove(removed);

        var problem = Assert.Single(MailConfiguration.Refusals(Load(values).Values));

        Assert.Equal((ConfigurationProblemKind.MissingKey, named), (problem.Kind, problem.Key));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("zqx")]
    [InlineData("-25")]
    public void Ca2APortThatIsNoneIsRefusedWithTheValueAndTheRule(string port)
    {
        var values = Whole();
        values["CDG_Mail__Port"] = port;

        var problem = Assert.Single(MailConfiguration.Refusals(Load(values).Values));

        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, MailConfiguration.PortPath, port), (problem.Kind, problem.Key, problem.Found));
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Plain")]
    [InlineData("tls")]
    public void Ca2ATransportInClearOrUnknownIsRefusedAtTheStartWithTheValuesAccepted(string transport)
    {
        var values = Whole();
        values["CDG_Mail__Transport"] = transport;

        var loaded = Load(values);

        Assert.True(loaded.Refused);
        var problem = Assert.Single(loaded.Problems);
        Assert.Equal((ConfigurationProblemKind.ValueNotAccepted, MailConfiguration.TransportPath), (problem.Kind, problem.Key));
        Assert.Equal(["Tls", "StartTls"], problem.Accepted!);
    }

    [Fact]
    public void Ca3ThePasswordIsShownByItsNameAndOriginNeverItsValue()
    {
        var loaded = Load(Whole());

        var shown = Assert.Single(loaded.View.Entries, entry => entry.Key == MailConfiguration.PasswordPath);

        Assert.Equal((EffectiveConfiguration.Redacted, ConfigurationOrigin.Environment), (shown.Value, shown.Origin));
        Assert.DoesNotContain(loaded.View.Entries, entry => entry.Value == "ZQX-SENHA-6351");
    }

    private static Dictionary<string, string?> Whole() => new()
    {
        ["CDG_Mail__Host"] = "zqx-smtp.example.test",
        ["CDG_Mail__Port"] = "587",
        ["CDG_Mail__Sender"] = "zqx@example.test",
        ["CDG_Mail__Transport"] = "StartTls",
        ["CDG_Mail__User"] = "zqx-user",
        ["CDG_Mail__Password"] = "ZQX-SENHA-6351",
    };

    private static ConfigurationLoadResult Load(Dictionary<string, string?> variables) =>
        ClosedConfiguration.Load(new ConfigurationSchema([MailConfiguration.Section]), new EnvironmentSource(variables));
}
