using Core.Platform.Configuration;

namespace Core.Tests.Properties.Configuration;

/// <summary>Loaders that break one invariant each, to show the property for it can fail.</summary>
internal static class BrokenLoaders
{
    /// <summary>Forgets every key the schema does not declare before loading.</summary>
    public static ConfigurationLoadResult IgnoringUnknownKeys(ConfigurationSchema schema, IEnumerable<IConfigurationSource> sources) =>
        ClosedConfiguration.Load(schema, [.. sources.Select(Known)]);

    /// <summary>Fills in the required keys from a source below every other.</summary>
    public static ConfigurationLoadResult FillingMissingKeys(ConfigurationSchema schema, IEnumerable<IConfigurationSource> sources) =>
        ClosedConfiguration.Load(schema, [.. sources, Fallback()]);

    /// <summary>Hands every source over as if it were the file.</summary>
    public static ConfigurationLoadResult HidingOrigins(ConfigurationSchema schema, IEnumerable<IConfigurationSource> sources) =>
        ClosedConfiguration.Load(schema, [.. sources.Select(Relabelled)]);

    private static FixedConfigurationSource Known(IConfigurationSource source)
    {
        var reading = source.Read();
        return new(
            reading.Entries.Where(entry => ConfigurationGenerators.DeclaredPaths.Contains(entry.Key, StringComparer.OrdinalIgnoreCase)),
            reading.Origin);
    }

    private static FixedConfigurationSource Relabelled(IConfigurationSource source) =>
        new(source.Read().Entries, ConfigurationOrigin.File);

    private static FixedConfigurationSource Fallback() =>
        new(
            [KeyValuePair.Create<string, string?>("Sample:Name", "fallback"), KeyValuePair.Create<string, string?>("Sample:Threshold", "1")],
            ConfigurationOrigin.Default);
}
