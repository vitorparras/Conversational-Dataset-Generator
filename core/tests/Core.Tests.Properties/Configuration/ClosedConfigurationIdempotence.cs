using Core.Platform.Configuration;
using Core.Tests.Properties.Harness;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>
/// What requisito 032 (docs/requisitos/032-configuracao-falha-fechada.md) needs of loading: the same sources
/// always give the same verdict, and a configuration that loaded does not change when its own values are loaded again.
/// </summary>
internal sealed class ClosedConfigurationIdempotence
{
    private readonly PropertyRunner runner;
    private readonly ConfigurationLoader load;

    public ClosedConfigurationIdempotence(PropertyRunner runner, ConfigurationLoader load)
    {
        this.runner = runner;
        this.load = load;
    }

    public void LoadingTheSameSourcesTwiceGivesTheSameResult() =>
        runner.Check(
            Gen.OneOf(ConfigurationGenerators.Arbitrary, ConfigurationGenerators.WellFormed),
            sources => Assert.Equal(
                LoadSnapshot.Of(load(ConfigurationGenerators.Schema, sources)),
                LoadSnapshot.Of(load(ConfigurationGenerators.Schema, sources))));

    public void LoadingTheLoadedValuesAgainKeepsThem() =>
        runner.Check(ConfigurationGenerators.WellFormed, sources =>
        {
            var first = load(ConfigurationGenerators.Schema, sources);
            var again = load(ConfigurationGenerators.Schema, [AsSource(first)]);

            Assert.False(first.Refused);
            Assert.Equal(LoadSnapshot.ValuesOf(first), LoadSnapshot.ValuesOf(again));
        });

    private static FixedConfigurationSource AsSource(ConfigurationLoadResult loaded) =>
        new(loaded.Values.Select(pair => KeyValuePair.Create<string, string?>(pair.Key, pair.Value)), ConfigurationOrigin.File);
}
