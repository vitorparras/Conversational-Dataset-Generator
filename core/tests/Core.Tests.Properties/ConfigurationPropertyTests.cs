using Core.Platform.Configuration;
using CsCheck;
using Microsoft.Extensions.Configuration.Memory;

namespace Core.Tests.Properties;

/// <summary>
/// Requirement 032 (the configuration is a closed set of keys): loading is a function of the
/// sources alone, and what it accepts it can present and accept again unchanged (CA-4, CA-1..3).
/// </summary>
public sealed class ConfigurationPropertyTests
{
    private static readonly string[] KnownKeys = ["Sample:Mode", "Sample:Threshold", "Sample:Port"];
    private static readonly string[] AllKeys = [.. KnownKeys, "Sample:Treshold", "Other:Key"];

    private static ConfigurationSchema Schema { get; } = new(
    [
        new ConfigurationSection(
            "Sample",
            [
                ConfigurationKey.WithDefault("Mode", "strict", acceptedValues: ["strict", "lenient"]),
                ConfigurationKey.Protection("Threshold"),
                ConfigurationKey.WithDefault("Port", "5080", ValueFormat.Port),
            ]),
    ]);

    // Values that are sometimes accepted and sometimes not, including the empty string and non-ASCII text.
    private static readonly Gen<string> Values = Gen.OneOf(
        Gen.OneOfConst("strict", "lenient", "3", "5080", "70000", "", " ", "ZQX-MARKER-7731"),
        Gen.String);

    private static readonly Gen<ConfigurationOrigin> Origins = Gen.OneOfConst(
        ConfigurationOrigin.File, ConfigurationOrigin.Environment, ConfigurationOrigin.Argument);

    private static readonly Gen<(string Key, string Value)> Entry = Gen.Select(Gen.OneOfConst(AllKeys), Values);

    private static readonly Gen<(ConfigurationOrigin Origin, (string Key, string Value)[] Entries)> SourceSpec =
        Gen.Select(Origins, Entry.Array[0, 4]);

    private static readonly Gen<(ConfigurationOrigin Origin, (string Key, string Value)[] Entries)[]> Sources =
        SourceSpec.Array[0, 3];

    [Fact]
    public void LoadingTheSameSourcesTwiceGivesTheSameOutcome()
    {
        PropertyRun.Check(Sources, specs =>
        {
            var first = ClosedConfiguration.Load(Build(specs), Schema);
            var second = ClosedConfiguration.Load(Build(specs), Schema);

            Assert.Equal(first.Refused, second.Refused);
            Assert.Equal(first.Problems, second.Problems);
            Assert.Equal(first.Configuration?.View(), second.Configuration?.View());
        });
    }

    [Fact]
    public void WhatLoadedPresentsLoadsAgainUnchanged()
    {
        PropertyRun.Check(Sources, specs =>
        {
            var loaded = ClosedConfiguration.Load(Build(specs), Schema);
            if (loaded.Refused)
            {
                return;
            }

            var shown = loaded.Configuration!.View();
            var again = ClosedConfiguration.Load(
                [Build([(ConfigurationOrigin.File, [.. shown.Select(e => (e.Key, e.Value!))])]).Single()],
                Schema);

            Assert.False(again.Refused, string.Join("; ", again.Problems.Select(p => p.Describe())));
            Assert.Equal(
                shown.Select(e => (e.Key, e.Value)),
                again.Configuration!.View().Select(e => (e.Key, e.Value)));
        });
    }

    [Fact]
    public void AnUnknownKeyRefusesWhateverElseTheSourcesHold()
    {
        PropertyRun.Check(Gen.Select(Sources, Values), pair =>
        {
            var (specs, value) = pair;
            var withTypo = specs.Append((ConfigurationOrigin.File, [("Sample:Treshold", value)])).ToArray();

            var result = ClosedConfiguration.Load(Build(withTypo), Schema);

            Assert.True(result.Refused);
            Assert.Contains(
                result.Problems,
                p => p.Kind == ConfigurationProblemKind.UnknownKey && p.Key == "Sample:Treshold");
        });
    }

    private static List<OriginatedSource> Build(
        IEnumerable<(ConfigurationOrigin Origin, (string Key, string Value)[] Entries)> specs) =>
        [.. specs.Select(spec => new OriginatedSource(
            spec.Origin,
            spec.Origin.ToString(),
            new MemoryConfigurationSource
            {
                // A source holds a key once; the last write wins, as in a settings file read top to bottom.
                InitialData = spec.Entries
                    .GroupBy(e => e.Key, StringComparer.Ordinal)
                    .Select(g => new KeyValuePair<string, string?>(g.Key, g.Last().Value)),
            }))];
}
