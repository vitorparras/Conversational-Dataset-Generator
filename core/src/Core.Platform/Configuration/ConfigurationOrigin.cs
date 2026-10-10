namespace Core.Platform.Configuration;

/// <summary>Where a configuration value came from.</summary>
/// <remarks>
/// The numeric order is the precedence: a value from a higher origin replaces one from a lower
/// origin. Arguments beat environment variables, which beat the file, which beats the default
/// the key declares. <see cref="ClosedConfiguration.Load"/> applies this order whatever order the
/// sources are given in.
/// </remarks>
public enum ConfigurationOrigin
{
    /// <summary>The default the key declares in its section.</summary>
    Default = 0,

    /// <summary>The JSON file next to the executable.</summary>
    File = 1,

    /// <summary>An environment variable with the <c>CDG__</c> prefix.</summary>
    Environment = 2,

    /// <summary>A command-line argument.</summary>
    Argument = 3,
}
