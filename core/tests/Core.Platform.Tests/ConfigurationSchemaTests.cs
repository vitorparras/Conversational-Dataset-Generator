using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>The schema unites the sections and refuses to be built with the same key twice.</summary>
public sealed class ConfigurationSchemaTests
{
    [Fact]
    public void SectionsAreUnitedInDeclarationOrder()
    {
        var schema = new ConfigurationSchema(
        [
            new ConfigurationSection("First", [ConfigurationKey.Required("A"), ConfigurationKey.Required("B")]),
            new ConfigurationSection("Second", [ConfigurationKey.Required("C")]),
        ]);

        Assert.Equal(["First:A", "First:B", "Second:C"], schema.Declared.Select(declared => declared.Path));
    }

    [Theory]
    [InlineData("Sample", "Key")]
    [InlineData("sample", "KEY")]
    public void KeyDeclaredTwiceIsADefect(string section, string key)
    {
        IReadOnlyList<ConfigurationSection> sections =
        [
            new ConfigurationSection("Sample", [ConfigurationKey.Required("Key")]),
            new ConfigurationSection(section, [ConfigurationKey.Required(key)]),
        ];

        var defect = Assert.Throws<ArgumentException>(() => new ConfigurationSchema(sections));

        Assert.Contains("declared twice", defect.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UndeclaredPathIsNotFound()
    {
        var schema = new ConfigurationSchema([new ConfigurationSection("Sample", [ConfigurationKey.Required("Key")])]);

        Assert.Null(schema.Find("Sample:Other"));
    }
}
