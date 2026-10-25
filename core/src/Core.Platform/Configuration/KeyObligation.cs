namespace Core.Platform.Configuration;

/// <summary>Whether a source must declare the key, and whether the key protects.</summary>
internal enum KeyObligation
{
    /// <summary>Takes its default, or stays without a value, when no source declares it.</summary>
    Optional = 0,

    /// <summary>Some source must declare it.</summary>
    Required = 1,

    /// <summary>Holds a protection: required, and nothing can give it a default.</summary>
    Protection = 2,
}
