using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>A source that hands over exactly the entries it was given, under the origin it was given.</summary>
internal sealed class FixedConfigurationSource : IConfigurationSource
{
    private readonly SourceReading reading;

    public FixedConfigurationSource(IEnumerable<KeyValuePair<string, string?>> entries, ConfigurationOrigin origin)
    {
        reading = SourceReading.Of(origin, entries);
    }

    public SourceReading Read() => reading;
}
