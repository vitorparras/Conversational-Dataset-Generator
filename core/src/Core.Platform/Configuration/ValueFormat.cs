namespace Core.Platform.Configuration;

/// <summary>The shape a configuration value must have.</summary>
public enum ValueFormat
{
    /// <summary>Any text.</summary>
    Text = 0,

    /// <summary>A TCP port, from 0 (the system picks a free one) to 65535.</summary>
    Port = 1,

    /// <summary>An IPv4 or IPv6 address.</summary>
    IpAddress = 2,

    /// <summary>A whole number of at least 1, with digits only.</summary>
    PositiveInteger = 3,
}
