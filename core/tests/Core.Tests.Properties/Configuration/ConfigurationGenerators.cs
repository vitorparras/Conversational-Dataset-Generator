using Core.Platform.Configuration;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>Sources for the sample schema: arbitrary ones that mostly get refused, and well-formed ones that always load.</summary>
internal static class ConfigurationGenerators
{
    public static readonly ConfigurationSchema Schema = new(
    [
        new ConfigurationSection(
            "Sample",
            [
                ConfigurationKey.Required("Name"),
                ConfigurationKey.Protection("Threshold"),
                ConfigurationKey.WithDefault("Mode", "strict").Accepting("strict", "lenient"),
                ConfigurationKey.WithDefault("Token", "none").AsSensitive(),
            ]),
    ]);

    private static readonly Gen<ConfigurationOrigin> Origin = Gen.Enum<ConfigurationOrigin>();

    private static readonly Gen<string> AnyKey = Gen.OneOfConst(
        "Sample:Name", "sample:name", "Sample:Threshold", "Sample:Mode", "Sample:Token", "Sample:Unknown", "Typo");

    private static readonly Gen<string?> AnyValue = Gen.OneOfConst(
        null, string.Empty, "   ", "strict", "lenient", "bogus", "ZQX-MARKER-7731", "3");

    private static readonly Gen<string?> OptionalName = Gen.OneOfConst(null, "ZQX-MARKER-7731", "alpha", "beta gamma");

    private static readonly Gen<string?> OptionalThreshold = Gen.OneOfConst(null, "3", "7", "0.5");

    private static readonly Gen<string?> OptionalMode = Gen.OneOfConst(null, "strict", "lenient");

    private static readonly Gen<string?> OptionalToken = Gen.OneOfConst(null, "ZQX-MARKER-7731", "other");

    private static readonly Gen<string> RequiredName = Gen.OneOfConst("ZQX-MARKER-7731", "alpha", "beta gamma");

    private static readonly Gen<string> RequiredThreshold = Gen.OneOfConst("3", "7", "0.5");

    private static readonly Gen<KeyValuePair<string, string?>> AnyEntry =
        Gen.Select(AnyKey, AnyValue, KeyValuePair.Create);

    /// <summary>Up to three sources of any origin writing any mix of declared, misspelled and empty entries.</summary>
    public static readonly Gen<List<IConfigurationSource>> Arbitrary =
        Gen.Select(Origin, AnyEntry.List[0, 4], Source).List[0, 3];

    /// <summary>One source that declares every required key, then up to two more overriding optional ones; always loads.</summary>
    public static readonly Gen<List<IConfigurationSource>> WellFormed =
        Gen.Select(Origin, RequiredName, RequiredThreshold, OptionalMode, OptionalToken, Declaring)
            .SelectMany(first => Gen.Select(Origin, OptionalName, OptionalThreshold, OptionalMode, OptionalToken, Declaring)
                .List[0, 2]
                .Select(more => new List<IConfigurationSource>([first, .. more])));

    private static IConfigurationSource Source(ConfigurationOrigin origin, List<KeyValuePair<string, string?>> entries) =>
        new FixedConfigurationSource(entries, origin);

    private static IConfigurationSource Declaring(
        ConfigurationOrigin origin, string? name, string? threshold, string? mode, string? token) =>
        Written(origin, ("Sample:Name", name), ("Sample:Threshold", threshold), ("Sample:Mode", mode), ("Sample:Token", token));

    private static FixedConfigurationSource Written(ConfigurationOrigin origin, params (string Key, string? Value)[] entries) =>
        new(entries.Where(entry => entry.Value is not null).Select(entry => KeyValuePair.Create(entry.Key, entry.Value)), origin);
}
