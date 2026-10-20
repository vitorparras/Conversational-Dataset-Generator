using Core.Platform.Configuration;
using ConfigurationSection = Core.Platform.Configuration.ConfigurationSection;

namespace Core.Host;

/// <summary>Which recognizer the core asks: the language service, unless the fake is chosen by its exact name.</summary>
internal static class RecognizerConfiguration
{
    internal const string KindPath = SectionName + ":" + KindKey;

    private const string SectionName = "Recognizer";
    private const string KindKey = "Kind";
    private const string Fake = "fake";
    private const string Http = "http";

    /// <summary>The real recognizer is the default, since a fake left on by omission would pass every real text unanalysed.</summary>
    internal static ConfigurationSection Section { get; } =
        new(SectionName, [ConfigurationKey.WithDefault(KindKey, Http).Accepting(Fake, Http)]);

    /// <summary>Whether the fake was chosen, which asks no language service.</summary>
    internal static bool UsesFake(IReadOnlyDictionary<string, string> values) =>
        string.Equals(values[KindPath], Fake, StringComparison.Ordinal);
}
