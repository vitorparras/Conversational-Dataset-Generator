using Core.Host.Endpoints;
using Core.Platform.Configuration;

namespace Core.Tests.Unit.Host;

/// <summary>Issue 25 CA-4: the endpoint shows the whole configuration, with origin, secret redacted.</summary>
public sealed class ConfigurationEndpointTests
{
    [Fact]
    public void EndpointShowsEveryKeyWithOriginAndRedactsTheSecret()
    {
        var schema = new ConfigurationSchema(
        [
            new ConfigurationSection(
                "Sample",
                [
                    ConfigurationKey.WithDefault("Plain", "visible"),
                    ConfigurationKey.Required("Secret", sensitive: true),
                ]),
        ]);
        var configuration = ClosedConfiguration.Load(
            [ConfigurationSources.Arguments(["--Sample:Secret=ZQX-MARKER-7731"])], schema).Configuration!;

        var response = ConfigurationEndpoint.Handle(configuration);

        Assert.Equal(
            [
                new ConfigurationEntryResponse("Sample:Plain", "visible", "default", Redacted: false),
                new ConfigurationEntryResponse("Sample:Secret", null, "argument", Redacted: true),
            ],
            response.Entries);
    }
}
