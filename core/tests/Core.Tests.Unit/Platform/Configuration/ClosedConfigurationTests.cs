using Core.Platform.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace Core.Tests.Unit.Platform.Configuration;

/// <summary>Issue 25 and requirement 032: the configuration is a closed set of keys.</summary>
public sealed class ClosedConfigurationTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cdg-config-");

    private static ConfigurationSchema Schema { get; } = new(
    [
        new ConfigurationSection(
            "Sample",
            [
                ConfigurationKey.WithDefault("Mode", "strict", acceptedValues: ["strict", "lenient"]),
                ConfigurationKey.Protection("Threshold"),
                ConfigurationKey.WithDefault("Token", "ZQX-MARKER-7731-default", sensitive: true),
                ConfigurationKey.WithDefault("Port", "5080", ValueFormat.Port),
            ]),
    ]);

    public void Dispose() => directory.Delete(recursive: true);

    [Fact]
    public void UnknownKeyInTheFileRefusesAndIsNamed()
    {
        var file = WriteFile("""{ "Sample": { "Threshold": "3", "Treshold": "3" } }""");

        var result = ClosedConfiguration.Load([file], Schema);

        Assert.True(result.Refused);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.UnknownKey, problem.Kind);
        Assert.Equal("Sample:Treshold", problem.Key);
        Assert.Equal(ConfigurationOrigin.File, problem.Origin);
        Assert.Contains("'Sample:Treshold'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownKeyInAnySourceRefuses()
    {
        var result = ClosedConfiguration.Load(
            [Memory(ConfigurationOrigin.Environment, ("Sample:Threshold", "3"), ("Other:Key", "x")),
             ConfigurationSources.Arguments(["--Sample:Nope=1"])],
            Schema);

        Assert.True(result.Refused);
        Assert.Equal(
            ["Other:Key", "Sample:Nope"],
            result.Problems.Where(p => p.Kind == ConfigurationProblemKind.UnknownKey).Select(p => p.Key));
    }

    [Fact]
    public void RequiredKeyMissingRefusesAndIsNamedWithoutDefault()
    {
        var result = ClosedConfiguration.Load([Memory(ConfigurationOrigin.File)], Schema);

        Assert.True(result.Refused);
        Assert.Null(result.Configuration);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("Sample:Threshold", problem.Key);
        Assert.Contains("'Sample:Threshold'", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProtectionKeyIsRequiredAndCarriesNoDefault()
    {
        var key = ConfigurationKey.Protection("Threshold");

        Assert.True(key.Protects);
        Assert.True(key.IsRequired);
        Assert.Null(key.DefaultValue);
    }

    [Fact]
    public void ValueOutsideTheAcceptedSetSaysWhatWasFoundAndWhatIsAccepted()
    {
        var result = ClosedConfiguration.Load(
            [Memory(ConfigurationOrigin.File, ("Sample:Threshold", "3"), ("Sample:Mode", "loose"))],
            Schema);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, problem.Kind);
        Assert.Equal("Sample:Mode", problem.Key);
        Assert.Equal("loose", problem.Found);
        Assert.Equal("strict, lenient", problem.Accepted);
        var message = problem.Describe();
        Assert.Contains("'loose'", message, StringComparison.Ordinal);
        Assert.Contains("strict, lenient", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValueOutsideTheFormatIsRefused()
    {
        var result = ClosedConfiguration.Load(
            [Memory(ConfigurationOrigin.File, ("Sample:Threshold", "3"), ("Sample:Port", "65536"))],
            Schema);

        var problem = Assert.Single(result.Problems);
        Assert.Equal(("Sample:Port", "65536"), (problem.Key, problem.Found));
    }

    [Fact]
    public void RefusedSensitiveValueIsNotEchoed()
    {
        var schema = new ConfigurationSchema(
            [new ConfigurationSection("Sample", [ConfigurationKey.Required("Key", acceptedValues: ["a"], sensitive: true)])]);

        var result = ClosedConfiguration.Load(
            [Memory(ConfigurationOrigin.File, ("Sample:Key", "ZQX-MARKER-7731"))], schema);

        var problem = Assert.Single(result.Problems);
        Assert.Null(problem.Found);
        Assert.DoesNotContain("ZQX-MARKER-7731", problem.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void ArgumentBeatsEnvironmentWhichBeatsFile()
    {
        var file = Memory(ConfigurationOrigin.File, ("Sample:Threshold", "1"), ("Sample:Mode", "lenient"));
        var environment = Memory(ConfigurationOrigin.Environment, ("Sample:Threshold", "2"));
        var arguments = ConfigurationSources.Arguments(["--Sample:Threshold", "3"]);

        // Given in reverse order: the precedence is the code's, not the caller's.
        var configuration = Loaded([arguments, environment, file]);

        Assert.Equal("3", configuration.Get("Sample:Threshold"));
        Assert.Equal(ConfigurationOrigin.Argument, configuration.OriginOf("Sample:Threshold"));
        Assert.Equal("lenient", configuration.Get("Sample:Mode"));
        Assert.Equal(ConfigurationOrigin.File, configuration.OriginOf("Sample:Mode"));
    }

    [Fact]
    public void EnvironmentBeatsFile()
    {
        var configuration = Loaded(
        [
            Memory(ConfigurationOrigin.Environment, ("Sample:Threshold", "2")),
            Memory(ConfigurationOrigin.File, ("Sample:Threshold", "1")),
        ]);

        Assert.Equal("2", configuration.Get("Sample:Threshold"));
        Assert.Equal(ConfigurationOrigin.Environment, configuration.OriginOf("Sample:Threshold"));
    }

    [Fact]
    public void ViewShowsEveryKeyWithItsOriginAndRedactsTheSensitive()
    {
        var configuration = Loaded([Memory(ConfigurationOrigin.Argument, ("Sample:Threshold", "3"))]);

        var view = configuration.View();

        Assert.Equal(
            [
                new EffectiveConfigurationEntry("Sample:Mode", "strict", ConfigurationOrigin.Default, false),
                new EffectiveConfigurationEntry("Sample:Threshold", "3", ConfigurationOrigin.Argument, false),
                new EffectiveConfigurationEntry("Sample:Token", null, ConfigurationOrigin.Default, true),
                new EffectiveConfigurationEntry("Sample:Port", "5080", ConfigurationOrigin.Default, false),
            ],
            view);
        Assert.Equal("ZQX-MARKER-7731-default", configuration.Get("Sample:Token"));
    }

    [Fact]
    public void KeysMatchIgnoringCaseAndReportTheDeclaredPath()
    {
        var configuration = Loaded([Memory(ConfigurationOrigin.File, ("sample:THRESHOLD", "3"))]);

        Assert.Contains(configuration.View(), e => e is { Key: "Sample:Threshold", Value: "3" });
    }

    [Fact]
    public void EnvironmentVariablesAreReadWithThePrefix()
    {
        var name = ConfigurationSources.EnvironmentPrefix + "Sample__Threshold";
        Environment.SetEnvironmentVariable(name, "7");
        try
        {
            var environment = ConfigurationSources.Standard(directory.FullName, [])
                .Single(s => s.Origin == ConfigurationOrigin.Environment);

            var configuration = Loaded([environment]);

            Assert.Equal("7", configuration.Get("Sample:Threshold"));
            Assert.Equal(ConfigurationOrigin.Environment, configuration.OriginOf("Sample:Threshold"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Theory]
    [InlineData("Sample:Threshold=3")]
    [InlineData("--")]
    [InlineData("--Sample:Threshold")]
    [InlineData("--=3")]
    public void MalformedArgumentRefusesInsteadOfBeingIgnored(string argument)
    {
        var result = ClosedConfiguration.Load([ConfigurationSources.Arguments([argument])], Schema);

        Assert.Contains(result.Problems, p => p.Kind == ConfigurationProblemKind.UnreadableSource);
    }

    [Fact]
    public void MalformedFileRefuses()
    {
        var result = ClosedConfiguration.Load([WriteFile("{ \"Sample\": ")], Schema);

        Assert.Contains(result.Problems, p => p.Kind == ConfigurationProblemKind.UnreadableSource && p.Origin == ConfigurationOrigin.File);
    }

    [Fact]
    public void KeyDeclaredTwiceIsADefectOfTheSchema() =>
        Assert.Throws<ArgumentException>(() => new ConfigurationSchema(
        [
            new ConfigurationSection("Sample", [ConfigurationKey.Required("Key")]),
            new ConfigurationSection("sample", [ConfigurationKey.Required("key")]),
        ]));

    private static EffectiveConfiguration Loaded(IReadOnlyList<OriginatedSource> sources)
    {
        var result = ClosedConfiguration.Load(sources, Schema);
        Assert.Empty(result.Problems);
        return result.Configuration!;
    }

    private static OriginatedSource Memory(ConfigurationOrigin origin, params (string Key, string Value)[] values) =>
        new(origin, origin.ToString(), new MemoryConfigurationSource
        {
            InitialData = values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)),
        });

    private OriginatedSource WriteFile(string json)
    {
        var path = Path.Combine(directory.FullName, ConfigurationSources.FileName);
        File.WriteAllText(path, json);
        return ConfigurationSources.JsonFile(path);
    }
}
