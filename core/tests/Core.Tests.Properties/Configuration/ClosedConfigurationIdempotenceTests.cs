using Core.Platform.Configuration;
using Core.Tests.Properties.Harness;
using CsCheck;

namespace Core.Tests.Properties.Configuration;

/// <summary>Loading is idempotent (requisito 032, docs/requisitos/032-configuracao-falha-fechada.md); a load that is not idempotent falls to the same properties.</summary>
public sealed class ClosedConfigurationIdempotenceTests
{
    private static ClosedConfigurationIdempotence Over(ConfigurationLoader load) => new(PropertyRunner.FromProcess(), load);

    private static ConfigurationLoadResult Production(ConfigurationSchema schema, IReadOnlyList<IConfigurationSource> sources) =>
        ClosedConfiguration.Load(schema, sources);

    private static ConfigurationLoader DriftingOnEverySecondCall()
    {
        var calls = 0;
        return (schema, sources) => Production(schema, ++calls % 2 == 0 ? [.. sources, Leniency()] : sources);
    }

    private static ConfigurationLoader DriftingOnASingleSource() =>
        (schema, sources) => Production(schema, sources.Count == 1 ? [.. sources, Leniency()] : sources);

    private static FixedConfigurationSource Leniency() =>
        new([KeyValuePair.Create<string, string?>("Sample:Mode", "lenient")], ConfigurationOrigin.Argument);

    [Fact]
    public void LoadingTheSameSourcesTwiceGivesTheSameResult() =>
        Over(Production).LoadingTheSameSourcesTwiceGivesTheSameResult();

    [Fact]
    public void LoadingTheLoadedValuesAgainKeepsThem() =>
        Over(Production).LoadingTheLoadedValuesAgainKeepsThem();

    [Fact]
    public void ALoadThatDriftsBetweenCallsFailsTheSameSourcesProperty() =>
        Assert.Throws<CsCheckException>(Over(DriftingOnEverySecondCall()).LoadingTheSameSourcesTwiceGivesTheSameResult);

    [Fact]
    public void ALoadThatChangesWhenFedItsOwnValuesFailsTheLoadedValuesProperty() =>
        Assert.Throws<CsCheckException>(Over(DriftingOnASingleSource()).LoadingTheLoadedValuesAgainKeepsThem);
}
