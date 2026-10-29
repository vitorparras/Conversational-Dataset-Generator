using System.Text.Json;

namespace Core.Host.Supervision;

/// <summary>The one line the service writes to its standard output once it listens: <c>{"event":"listening","port":N}</c>.</summary>
internal static class PortAnnouncement
{
    private const string Event = "listening";
    private const int MinPort = 1;
    private const int MaxPort = 65535;

    /// <summary>The port in the line, or null when the line is anything else; a line the core cannot read names no port to talk to.</summary>
    internal static int? PortIn(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            return PortOf(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int? PortOf(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !IsListening(root))
        {
            return null;
        }

        if (!root.TryGetProperty("port", out var port) || port.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return port.TryGetInt32(out var number) && number is >= MinPort and <= MaxPort ? number : null;
    }

    private static bool IsListening(JsonElement root) =>
        root.TryGetProperty("event", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == Event;
}
