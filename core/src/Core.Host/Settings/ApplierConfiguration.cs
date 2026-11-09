using Core.Persistence.Connection;
using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host.Settings;

/// <summary>
/// The configuration of the applier, read from a file of its own beside the binary that only its account reads (decision 0053): the file of the core it writes, the command that restarts the core
/// and the credential of the database that only reads. A key missing, or a path that is not absolute or does not exist, refuses the start naming the key; nothing is guessed.
/// </summary>
internal static class ApplierConfiguration
{
    internal const string SettingsFilePath = SectionName + ":" + SettingsFileKey;

    internal const string RestartCommandPath = SectionName + ":" + RestartCommandKey;

    internal const string RestartArgumentsPath = SectionName + ":" + RestartArgumentsKey;

    /// <summary>The character that separates the arguments of the restart command, which is run with no shell.</summary>
    internal const char ArgumentSeparator = '|';

    private const string SectionName = "Applier";
    private const string SettingsFileKey = "SettingsFile";
    private const string RestartCommandKey = "RestartCommand";
    private const string RestartArgumentsKey = "RestartArguments";
    private const string AcceptedPath = "caminho absoluto de um arquivo que existe";

    /// <summary>The file of the applier, beside the binary.</summary>
    internal static string FilePath => Path.Combine(AppContext.BaseDirectory, "applier.json");

    /// <summary>The keys the applier declares, with the database it reads.</summary>
    internal static ConfigurationSchema Schema { get; } = new(
        new[]
        {
            new ConfigurationSection(SectionName, new[] { ConfigurationKey.Required(SettingsFileKey), ConfigurationKey.Required(RestartCommandKey), ConfigurationKey.Optional(RestartArgumentsKey) }),
            DatabaseConfiguration.Section
        });

    /// <summary>What refuses the paths the applier was given: both must be absolute and exist.</summary>
    internal static IReadOnlyList<ConfigurationProblem> Refusals(IReadOnlyDictionary<string, string> values)
    {
        var refusals = new List<ConfigurationProblem>();
        foreach (var path in new[] { SettingsFilePath, RestartCommandPath })
        {
            if (values.TryGetValue(path, out var found) && !(Path.IsPathFullyQualified(found) && File.Exists(found)))
            {
                refusals.Add(new ConfigurationProblem(ConfigurationProblemKind.ValueNotAccepted, path, found, new[] { AcceptedPath }.AsReadOnly()));
            }
        }

        return refusals;
    }

    /// <summary>The arguments of the restart command, none when the key is absent.</summary>
    internal static IReadOnlyList<string> RestartArguments(IReadOnlyDictionary<string, string> values)
    {
        var arguments = new List<string>();
        if (values.TryGetValue(RestartArgumentsPath, out var text))
        {
            arguments.AddRange(text.Split(ArgumentSeparator, StringSplitOptions.RemoveEmptyEntries));
        }

        return arguments;
    }
}
