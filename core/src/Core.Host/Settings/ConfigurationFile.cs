using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Core.Host.Settings;

/// <summary>
/// The file of the configuration as the applier writes it (decision 0053): one key changes and nothing else does. The new file is written beside the old one and takes its place in one step, with
/// the old one kept under a name that carries the moment, so a failure half way leaves the file as it was and the version before the change is never lost. It is the only code that writes the file.
/// </summary>
internal sealed class ConfigurationFile
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly string path;
    private readonly TimeProvider time;

    /// <summary>The file at the path given, and the clock that names the version kept.</summary>
    internal ConfigurationFile(string path, TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(time);
        this.path = path;
        this.time = time;
    }

    /// <summary>
    /// Sets the key, as <c>Section:Name</c>, to the value and keeps the version before. False, with the file untouched, when the file is not a JSON object or the section is not an object.
    /// </summary>
    internal bool TryChange(string key, string value)
    {
        var parts = key.Split(':');
        if (parts.Length != 2 || Read() is not { } root || !TrySet(root, parts[0], parts[1], value))
        {
            return false;
        }

        var written = path + ".new";
        Create(written, root.ToJsonString(Indented));
        Replace(written);
        return true;
    }

    private static bool TrySet(JsonObject root, string section, string name, string value)
    {
        if (root[section] is not (null or JsonObject))
        {
            return false;
        }

        var target = root[section] as JsonObject ?? new JsonObject();
        target[name] = value;
        root[section] = target;
        return true;
    }

    private JsonObject? Read()
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject : new JsonObject();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Writes the new file already with the mode of the old one, or readable by its owner alone when there is none, so that it is never readable by others, not even for an instant.</summary>
    private void Create(string written, string content)
    {
        File.Delete(written);
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = File.Exists(path) ? File.GetUnixFileMode(path) : UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        using var stream = new FileStream(written, options);
        stream.Write(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content));
    }

    private void Replace(string written)
    {
        if (!File.Exists(path))
        {
            File.Move(written, path);
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(written, File.GetUnixFileMode(path));
        }

        var moment = time.GetUtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        File.Replace(written, path, $"{path}.{moment}.previous");
    }
}
