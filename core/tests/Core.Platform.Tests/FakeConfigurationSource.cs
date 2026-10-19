using Core.Platform.Configuration;

namespace Core.Platform.Tests;

/// <summary>A test double for the source: it hands over exactly the reading it was given.</summary>
internal sealed class FakeConfigurationSource : IConfigurationSource
{
    private readonly SourceReading reading;

    public FakeConfigurationSource(IEnumerable<KeyValuePair<string, string?>> entries, ConfigurationOrigin origin)
    {
        reading = new SourceReading(origin, [.. entries], []);
    }

    public FakeConfigurationSource(ConfigurationOrigin origin, ConfigurationProblem problem)
    {
        reading = SourceReading.Unreadable(origin, problem);
    }

    public SourceReading Read() => reading;
}
