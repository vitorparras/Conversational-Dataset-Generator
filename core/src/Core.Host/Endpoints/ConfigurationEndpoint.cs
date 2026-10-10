using Core.Platform.Configuration;

namespace Core.Host.Endpoints;

/// <summary>One key of the configuration in force, as the endpoint shows it.</summary>
internal sealed record ConfigurationEntryResponse(string Key, string? Value, string Origin, bool Redacted);

/// <summary>The configuration in force, whole, with the origin of each value.</summary>
internal sealed record ConfigurationResponse(IReadOnlyList<ConfigurationEntryResponse> Entries);

/// <summary>Shows the configuration in force (issue 25 CA-4).</summary>
internal static class ConfigurationEndpoint
{
    public const string Route = "/configuration";

    public static ConfigurationResponse Handle(EffectiveConfiguration configuration) =>
        new([.. configuration.View().Select(entry => new ConfigurationEntryResponse(
            entry.Key, entry.Value, OriginName(entry.Origin), entry.Redacted))]);

    public static void Map(IEndpointRouteBuilder endpoints, EffectiveConfiguration configuration) =>
        endpoints.MapGet(Route, () => Handle(configuration));

    private static string OriginName(ConfigurationOrigin origin) => origin switch
    {
        ConfigurationOrigin.Default => "default",
        ConfigurationOrigin.File => "file",
        ConfigurationOrigin.Environment => "environment",
        ConfigurationOrigin.Argument => "argument",
        _ => throw new InvalidOperationException($"unknown origin {origin}"),
    };
}
