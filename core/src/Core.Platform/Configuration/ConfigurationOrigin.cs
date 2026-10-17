namespace Core.Platform.Configuration;

/// <summary>Where a value in force came from; a source later in this order overrides every source before it.</summary>
internal enum ConfigurationOrigin
{
    /// <summary>No source wrote the key; the value is the declared default.</summary>
    Default = 0,

    /// <summary>The configuration file.</summary>
    File = 1,

    /// <summary>An environment variable; overrides the file.</summary>
    Environment = 2,

    /// <summary>A command-line argument; overrides the environment and the file.</summary>
    Argument = 3,
}
