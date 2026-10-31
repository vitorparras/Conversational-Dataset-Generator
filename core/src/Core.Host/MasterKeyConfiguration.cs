using Core.Platform.Configuration;
using Core.Platform.Keys;
using Core.Platform.Startup;
using Microsoft.Extensions.DependencyInjection;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>
/// Where the custody files are: the paths of the file that holds the key and of the one that holds its labels, never the key;
/// they have no default, because a default would put the key where nobody chose.
/// </summary>
internal static class MasterKeyConfiguration
{
    internal const string FilePath = SectionName + ":" + FileKey;

    internal const string LabelsFilePath = SectionName + ":" + LabelsFileKey;

    private const string SectionName = "MasterKey";
    private const string FileKey = "File";
    private const string LabelsFileKey = "LabelsFile";

    internal static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.Protection(FileKey).AsSensitive(), ConfigurationKey.Protection(LabelsFileKey).AsSensitive()]);

    /// <summary>The same section for the migration mode, which holds no data of the key's: the paths may be given, and are then checked as text, but are not required.</summary>
    internal static ConfigurationSection SectionWhenMigrating { get; } =
        new(SectionName, [ConfigurationKey.Optional(FileKey).AsSensitive(), ConfigurationKey.Optional(LabelsFileKey).AsSensitive()]);

    /// <summary>The source of the files the approved values name, which refuses any that lies in the folder the host runs from.</summary>
    internal static FileMasterKeySource SourceOf(IReadOnlyDictionary<string, string> values) =>
        new(values[FilePath], values[LabelsFilePath], AppContext.BaseDirectory);

    /// <summary>The source of the approved values and the check that reads it, which refuses the start when the key is not what it must be.</summary>
    internal static void AddCheck(IServiceCollection services, IReadOnlyDictionary<string, string> values)
    {
        _ = services.AddSingleton<IMasterKeySource>(SourceOf(values));
        _ = services.AddSingleton<IStartupCheck>(provider => new MasterKeyCheck(provider.GetRequiredService<IMasterKeySource>()));
    }
}
