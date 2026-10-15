using System.Text.Json;

namespace Core.Platform.Configuration.Sources;

/// <summary>Reads one JSON file, nested objects flattened into <c>Section:Key</c> paths; an absent file writes nothing.</summary>
public sealed class JsonFileSource : IConfigurationSource
{
    private const char PathSeparator = ':';
    private readonly string path;

    /// <summary>Reads the file at the path, resolved against the working directory when relative.</summary>
    public JsonFileSource(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        this.path = path;
    }

    /// <inheritdoc />
    public SourceReading Read()
    {
        if (!File.Exists(path))
        {
            return SourceReading.Of(ConfigurationOrigin.File, []);
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return SourceReading.Of(ConfigurationOrigin.File, Flatten(document.RootElement, prefix: null));
        }
        catch (JsonException)
        {
            return SourceReading.Unreadable(
                ConfigurationOrigin.File,
                new ConfigurationProblem(ConfigurationProblemKind.UnreadableFile, path, Found: null, Accepted: null));
        }
    }

    private static IEnumerable<KeyValuePair<string, string?>> Flatten(JsonElement element, string? prefix) =>
        element.ValueKind == JsonValueKind.Object
            ? element.EnumerateObject().SelectMany(property => Flatten(property.Value, Join(prefix, property.Name)))
            : [KeyValuePair.Create(prefix ?? string.Empty, Scalar(element))];

    private static string Join(string? prefix, string name) => prefix is null ? name : $"{prefix}{PathSeparator}{name}";

    /// <summary>Text as written for a string, raw JSON for anything else, null for null.</summary>
    private static string? Scalar(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
}
