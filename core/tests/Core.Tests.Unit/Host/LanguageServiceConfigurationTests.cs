using Core.Host.Supervision;
using Core.Platform.Configuration;
using Microsoft.Extensions.Configuration.Memory;

namespace Core.Tests.Unit.Host;

/// <summary>Requirement 032 applied to the keys of the supervision: the command protects, so it has no default.</summary>
public sealed class LanguageServiceConfigurationTests
{
    private static readonly ConfigurationSchema Schema = new([LanguageServiceConfiguration.Section]);

    [Fact]
    public void WithoutTheCommandTheConfigurationIsRefusedAndTheKeyIsNamed()
    {
        var result = ClosedConfiguration.Load([Source()], Schema);

        Assert.True(result.Refused);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.MissingKey, problem.Kind);
        Assert.Equal("LanguageService:Command", problem.Key);
    }

    [Fact]
    public void TheCommandIsAProtectionSoItHasNoDefaultAndIsRequired()
    {
        var command = Schema["LanguageService:Command"];

        Assert.True(command.Protects);
        Assert.True(command.IsRequired);
        Assert.Null(command.DefaultValue);
    }

    [Fact]
    public void ArgumentsAndTimeoutHaveDefaultsThatAreNotProtections()
    {
        var result = ClosedConfiguration.Load([Source(("LanguageService:Command", "svc"))], Schema);

        var options = LanguageServiceOptions.FromConfiguration(result.Configuration!);
        Assert.Equal("svc", options.Command);
        Assert.Empty(options.Arguments);
        Assert.Equal(TimeSpan.FromSeconds(30), options.StartupTimeout);
        Assert.Equal(ConfigurationOrigin.Default, result.Configuration!.OriginOf("LanguageService:StartupTimeoutSeconds"));
    }

    [Fact]
    public void ArgumentsAreSplitOnSemicolonsAndTrimmedWithEmptyEntriesDropped()
    {
        var result = ClosedConfiguration.Load(
            [Source(("LanguageService:Command", "uv"), ("LanguageService:Arguments", "run; --frozen ;;python;-m;language_service;"))],
            Schema);

        var options = LanguageServiceOptions.FromConfiguration(result.Configuration!);

        Assert.Equal(["run", "--frozen", "python", "-m", "language_service"], options.Arguments);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public void TimeoutThatIsNotAPositiveWholeNumberIsRefused(string value)
    {
        var result = ClosedConfiguration.Load(
            [Source(("LanguageService:Command", "svc"), ("LanguageService:StartupTimeoutSeconds", value))],
            Schema);

        Assert.True(result.Refused);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("LanguageService:StartupTimeoutSeconds", problem.Key);
        Assert.Equal(ConfigurationProblemKind.ValueNotAccepted, problem.Kind);
    }

    [Fact]
    public void ExtraKeyInTheSectionIsUnknown()
    {
        var result = ClosedConfiguration.Load(
            [Source(("LanguageService:Command", "svc"), ("LanguageService:Host", "0.0.0.0"))],
            Schema);

        Assert.True(result.Refused);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(ConfigurationProblemKind.UnknownKey, problem.Kind);
        Assert.Equal("LanguageService:Host", problem.Key);
    }

    private static OriginatedSource Source(params (string Key, string Value)[] values) =>
        new(
            ConfigurationOrigin.File,
            "test",
            new MemoryConfigurationSource { InitialData = values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)) });
}
