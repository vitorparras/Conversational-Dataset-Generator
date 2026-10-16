namespace Core.Platform.Configuration;

/// <summary>Why loading refused a key.</summary>
internal enum ConfigurationProblemKind
{
    /// <summary>The source writes a key no section declares.</summary>
    UnknownKey = 0,

    /// <summary>A required key no source writes.</summary>
    MissingKey = 1,

    /// <summary>A declared key written with no value.</summary>
    EmptyValue = 2,

    /// <summary>A value outside what the key accepts.</summary>
    ValueNotAccepted = 3,
}
