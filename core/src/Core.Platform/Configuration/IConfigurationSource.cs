namespace Core.Platform.Configuration;

/// <summary>Where configuration comes from; each realisation reads one place and hands over what it found.</summary>
internal interface IConfigurationSource
{
    /// <summary>Every key the source writes with its value; null when the key is written without one.</summary>
    IReadOnlyList<KeyValuePair<string, string?>> Read();
}
