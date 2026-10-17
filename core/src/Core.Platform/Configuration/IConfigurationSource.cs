namespace Core.Platform.Configuration;

/// <summary>Where configuration comes from; each realisation reads one place and hands over what it found.</summary>
public interface IConfigurationSource
{
    /// <summary>Every key the source writes with its value (null when written without one), and every problem met reading.</summary>
    SourceReading Read();
}
