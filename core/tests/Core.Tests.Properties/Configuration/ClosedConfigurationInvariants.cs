using Core.Platform.Configuration;
using Core.Tests.Properties.Harness;

namespace Core.Tests.Properties.Configuration;

/// <summary>The invariants requisito 032 (docs/requisitos/032-configuracao-falha-fechada.md) writes about loading, each as a property over generated sources.</summary>
internal sealed class ClosedConfigurationInvariants
{
    private readonly PropertyRunner runner;
    private readonly ConfigurationLoader load;

    public ClosedConfigurationInvariants(PropertyRunner runner, ConfigurationLoader load)
    {
        this.runner = runner;
        this.load = load;
    }

    /// <summary>CA-1 of requisito 032: a key no section declares is refused, and the refusal names it.</summary>
    public void UnknownKeyIsRefusedAndNamed() =>
        runner.Check(ConfigurationGenerators.WithUnknownKey, generated =>
        {
            var result = load(ConfigurationGenerators.Schema, generated.Sources);

            var problem = Assert.Single(result.Problems);
            Assert.Equal((ConfigurationProblemKind.UnknownKey, generated.Key), (problem.Kind, problem.Key));
            Assert.Contains($"'{generated.Key}'", problem.Describe(), StringComparison.Ordinal);
        });

    /// <summary>CA-2 of requisito 032: a required key no source declares is refused, and the refusal names what is missing.</summary>
    public void MissingRequiredKeyIsRefusedAndNamed() =>
        runner.Check(ConfigurationGenerators.WithoutARequiredKey, generated =>
        {
            var result = load(ConfigurationGenerators.Schema, generated.Sources);

            var problem = Assert.Single(result.Problems);
            Assert.Equal((ConfigurationProblemKind.MissingKey, generated.Path), (problem.Kind, problem.Key));
            Assert.Contains($"'{generated.Path}'", problem.Describe(), StringComparison.Ordinal);
        });

    /// <summary>
    /// CA-4 of requisito 032: a configuration that loaded shows every declared key, each with a value some source of
    /// that origin wrote, or its default under the default origin, or no value under the default origin for the
    /// optional key without a default; the sensitive one is shown redacted.
    /// </summary>
    public void LoadedValuesShowTheirOrigin() =>
        runner.Check(ConfigurationGenerators.WellFormed, sources =>
        {
            var result = load(ConfigurationGenerators.Schema, sources);

            Assert.False(result.Refused);
            Assert.Equal(ConfigurationGenerators.DeclaredPaths, result.View.Entries.Select(entry => entry.Key));
            Assert.Empty(LoadSnapshot.UsedBy(result).Except(PossibleLines(sources, redact: false)));
            Assert.Empty(LoadSnapshot.ShownBy(result).Except(PossibleLines(sources, redact: true)));
        });

    private static IEnumerable<string> PossibleLines(IEnumerable<IConfigurationSource> sources, bool redact) =>
        WrittenLines(sources).Concat(DefaultLines()).Concat(UnsetLines()).Select(line => redact ? Redacted(line) : line);

    private static IEnumerable<string> WrittenLines(IEnumerable<IConfigurationSource> sources) =>
        sources
            .Select(source => source.Read())
            .SelectMany(reading => reading.Entries.Select(entry => LoadSnapshot.Line(PathOf(entry.Key), entry.Value, reading.Origin)));

    private static IEnumerable<string> DefaultLines() =>
        ConfigurationGenerators.Defaults.Select(pair => LoadSnapshot.Line(pair.Key, pair.Value, ConfigurationOrigin.Default));

    private static IEnumerable<string> UnsetLines() =>
        ConfigurationGenerators.WithoutDefault.Select(path => LoadSnapshot.Line(path, value: null, ConfigurationOrigin.Default));

    private static string Redacted(string line) =>
        line.StartsWith(ConfigurationGenerators.SensitivePath + "=", StringComparison.Ordinal)
            ? LoadSnapshot.Line(ConfigurationGenerators.SensitivePath, EffectiveConfiguration.Redacted, OriginOf(line))
            : line;

    private static ConfigurationOrigin OriginOf(string line) => Enum.Parse<ConfigurationOrigin>(line[(line.LastIndexOf(' ') + 1)..]);

    private static string PathOf(string key) =>
        ConfigurationGenerators.DeclaredPaths.Single(path => string.Equals(path, key, StringComparison.OrdinalIgnoreCase));
}
