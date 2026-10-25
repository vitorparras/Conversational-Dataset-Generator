namespace Core.Host.Tests.Privacy;

/// <summary>A place that holds the marker and the line that shows it; the text names both, so a failed sweep says where to look.</summary>
internal sealed record LogLeak(string Destination, string Line)
{
    public override string ToString() => $"{Destination}: {Line}";
}
