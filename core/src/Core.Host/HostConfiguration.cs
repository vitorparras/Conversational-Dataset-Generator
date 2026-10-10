using Core.Platform.Configuration;

namespace Core.Host;

/// <summary>The keys the host itself declares.</summary>
internal static class HostConfiguration
{
    public const string Address = "Server:Address";
    public const string Port = "Server:Port";

    /// <summary>Where the host listens. Loopback by default: nothing reaches it from outside the machine.</summary>
    public static ConfigurationSection Server { get; } = new(
        "Server",
        [
            ConfigurationKey.WithDefault("Address", "127.0.0.1", ValueFormat.IpAddress),
            ConfigurationKey.WithDefault("Port", "5080", ValueFormat.Port),
        ]);
}
