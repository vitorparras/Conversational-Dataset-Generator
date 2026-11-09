using Core.Platform.Configuration;
using IConfigurationSource = Core.Platform.Configuration.IConfigurationSource;

namespace Core.Host.Settings;

/// <summary>
/// The configuration in force with one key changed, read as the file would hand it over: the values the host started with, and the new value in place of the old one.
/// It reads nothing from disk and writes nothing; the trial of a change loads it with the same schema and rules as the start.
/// </summary>
internal sealed class TrialSource : IConfigurationSource
{
    private readonly IReadOnlyDictionary<string, string> inForce;
    private readonly string key;
    private readonly string value;

    /// <summary>The values in force, the key changed and its new value.</summary>
    internal TrialSource(IReadOnlyDictionary<string, string> inForce, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(inForce);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        this.inForce = inForce;
        this.key = key;
        this.value = value;
    }

    /// <summary>Every value in force, the changed key with its new value.</summary>
    public SourceReading Read()
    {
        var entries = inForce.Where(entry => !string.Equals(entry.Key, key, StringComparison.Ordinal))
            .Select(entry => new KeyValuePair<string, string?>(entry.Key, entry.Value))
            .Append(new KeyValuePair<string, string?>(key, value))
            .ToList();
        return new SourceReading(ConfigurationOrigin.File, entries, new List<ConfigurationProblem>().AsReadOnly());
    }
}
