using Core.Host.Screens;

namespace Core.Host.Tests;

/// <summary>
/// The names in words of the sections on the screen of the configuration (issue 734, configuration 4): every section the host declares has one, so that a section added without its name
/// fails here and never reaches the screen in English.
/// </summary>
public sealed class ConfigurationSectionLabelsTests
{
    private static readonly string[] Arguments =
    [
        "--Database:Host=127.0.0.1:5432",
        "--Database:Name=zqx",
        "--Database:Username=zqx",
        "--Database:Password=ZQX-SENHA-7340",
    ];

    [Fact]
    public void EverySectionTheHostDeclaresHasAName()
    {
        var loaded = HostConfiguration.Load(Arguments, migrating: true);

        var sections = loaded.View.Entries.Select(entry => entry.Key.Split(':')[0]).Distinct(StringComparer.Ordinal).ToList();

        Assert.False(loaded.Refused);
        Assert.NotEmpty(sections);
        Assert.All(sections, section => Assert.NotEqual(section, ConfigurationScreen.SectionTitle(section)));
    }

    [Theory]
    [InlineData("Tls")]
    [InlineData("Mail")]
    public void ASectionTheScreenHasToNameIsNamedInWordsWithTheNameOfTheFileBesideIt(string section)
    {
        var label = ConfigurationScreen.SectionTitle(section);

        Assert.EndsWith($"({section})", label, StringComparison.Ordinal);
    }
}
