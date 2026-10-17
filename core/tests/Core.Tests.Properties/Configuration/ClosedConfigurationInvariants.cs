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

    /// <summary>CA-4 of requisito 032: a configuration that loaded shows every declared key with the origin it came from.</summary>
    public void LoadedValuesShowTheirOrigin() =>
        runner.Check(ConfigurationGenerators.WellFormed, sources =>
        {
            var result = load(ConfigurationGenerators.Schema, sources);

            Assert.False(result.Refused);
            Assert.Equal(ExpectedOrigins(sources), LoadSnapshot.OriginsOf(result));
        });

    private static string ExpectedOrigins(IReadOnlyList<IConfigurationSource> sources)
    {
        var readings = sources.Select(source => source.Read()).ToList();
        return string.Join('\n', ConfigurationGenerators.DeclaredPaths.Select(path => LoadSnapshot.Line(path, OriginOf(path, readings))));
    }

    private static ConfigurationOrigin OriginOf(string path, IEnumerable<SourceReading> readings) =>
        readings
            .Where(reading => reading.Entries.Any(entry => string.Equals(entry.Key, path, StringComparison.OrdinalIgnoreCase)))
            .Select(reading => reading.Origin)
            .DefaultIfEmpty(ConfigurationOrigin.Default)
            .Max();
}
