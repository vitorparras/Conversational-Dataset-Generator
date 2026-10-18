using Core.Platform.Configuration;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>Sources for the sample schema: well-formed ones that always load, then the same with one key added or taken out.</summary>
internal static class ConfigurationGenerators
{
    public const string SensitivePath = "Sample:Token";

    public const string DefaultMode = "strict";

    public const string DefaultToken = "none";

    public static readonly ConfigurationSchema Schema = new(
    [
        new ConfigurationSection(
            "Sample",
            [
                ConfigurationKey.Required("Name"),
                ConfigurationKey.Protection("Threshold"),
                ConfigurationKey.WithDefault("Mode", DefaultMode).Accepting("strict", "lenient"),
                ConfigurationKey.WithDefault("Token", DefaultToken).AsSensitive(),
            ]),
    ]);

    /// <summary>The default of every declared path that has one.</summary>
    public static readonly IReadOnlyDictionary<string, string> Defaults =
        new Dictionary<string, string> { ["Sample:Mode"] = DefaultMode, ["Sample:Token"] = DefaultToken };

    /// <summary>Every declared path, in declaration order.</summary>
    public static readonly IReadOnlyList<string> DeclaredPaths =
        ["Sample:Name", "Sample:Threshold", "Sample:Mode", "Sample:Token"];

    private static readonly Gen<ConfigurationOrigin> Origin = Gen.OneOfConst(ConfigurationOrigin.File, ConfigurationOrigin.Environment, ConfigurationOrigin.Argument);

    private static readonly Gen<string?> OptionalName = Gen.OneOfConst(null, "ZQX-MARKER-7731", "alpha", "beta gamma");

    private static readonly Gen<string?> OptionalThreshold = Gen.OneOfConst(null, "3", "7", "0.5");

    private static readonly Gen<string?> OptionalMode = Gen.OneOfConst(null, "strict", "lenient");

    private static readonly Gen<string?> OptionalToken = Gen.OneOfConst(null, "ZQX-MARKER-7731", "other");

    private static readonly Gen<string> RequiredName = Gen.OneOfConst("ZQX-MARKER-7731", "alpha", "beta gamma");

    private static readonly Gen<string> RequiredThreshold = Gen.OneOfConst("3", "7", "0.5");

    private static readonly Gen<string> UnknownKey = Gen.OneOfConst("Sample:Treshold", "Typo", "Other:Name", "sample:nmae");

    private static readonly Gen<string> RequiredPath = Gen.OneOfConst("Sample:Name", "Sample:Threshold");

    /// <summary>One source that declares every required key, then up to two more overriding optional ones; always loads.</summary>
    public static readonly Gen<List<IConfigurationSource>> WellFormed =
        Gen.Select(Origin, Gen.Select(RequiredName, RequiredThreshold), Gen.Select(OptionalMode, OptionalToken), Declaring)
            .SelectMany(first => Gen.Select(Origin, Gen.Select(OptionalName, OptionalThreshold), Gen.Select(OptionalMode, OptionalToken), Declaring)
                .List[0, 2]
                .Select(more => new List<IConfigurationSource>([first, .. more])));

    /// <summary>Well-formed sources and one more, of any origin, writing a key no section declares.</summary>
    public static readonly Gen<UnknownKeyCase> WithUnknownKey =
        Gen.Select(WellFormed, UnknownKey, Origin, (sources, key, origin) =>
            new UnknownKeyCase([.. sources, new FixedConfigurationSource([KeyValuePair.Create<string, string?>(key, "ZQX-MARKER-7731")], origin)], key));

    /// <summary>Well-formed sources with one required key taken out of every one of them.</summary>
    public static readonly Gen<MissingKeyCase> WithoutARequiredKey =
        Gen.Select(WellFormed, RequiredPath, (sources, path) =>
            new MissingKeyCase([.. sources.Select(source => Without(source, path))], path));

    private static IConfigurationSource Declaring(
        ConfigurationOrigin origin, (string? Name, string? Threshold) identity, (string? Mode, string? Token) tuning) =>
        Written(origin, ("Sample:Name", identity.Name), ("Sample:Threshold", identity.Threshold), ("Sample:Mode", tuning.Mode), ("Sample:Token", tuning.Token));

    private static FixedConfigurationSource Written(ConfigurationOrigin origin, params (string Key, string? Value)[] entries) =>
        new(entries.Where(entry => entry.Value is not null).Select(entry => KeyValuePair.Create(entry.Key, entry.Value)), origin);

    private static FixedConfigurationSource Without(IConfigurationSource source, string path)
    {
        var reading = source.Read();
        return new(reading.Entries.Where(entry => !string.Equals(entry.Key, path, StringComparison.OrdinalIgnoreCase)), reading.Origin);
    }
}
